namespace Dmx2Mqtt.Audio;

/// <param name="Bpm">Smoothed tempo</param>
/// <param name="SinceBeat">How long before the end of the processed audio the last beat was</param>
/// <param name="Confidence">How periodic the onsets are, 0-1</param>
public readonly record struct BeatEstimate(double Bpm, TimeSpan SinceBeat, double Confidence);

/// <summary>
/// Finds the tempo and the beat phase in mono audio.
///
/// Onsets are the spectral flux of log magnitudes (how much louder each frequency got
/// since the previous frame). The tempo is the period whose multiples correlate best in
/// the last few seconds of onsets, weighted towards 120 BPM against picking half or
/// double the tempo. The phase is where a comb of beats at that tempo lines up best
/// with the latest onsets.
/// </summary>
public sealed class BeatDetector
{
	public const double MinBpm = 70;
	public const double MaxBpm = 180;

	private const int FftSize = 1024;
	private const double HopSeconds = 0.0116;
	private const double HistorySeconds = 8;
	private const double MinHistorySeconds = 4;
	private const double EstimateSeconds = 0.5;
	private const double PreferredBpm = 120;
	private const double PriorOctaves = 0.5;
	private const double MaxFrequency = 8000;

	/// <summary>
	/// The kick drum. This band counts as much as everything above it, since the kick is
	/// what the beat is in most dance music, and a broadband snare otherwise dominates.
	/// </summary>
	private const double BassFrequency = 200;
	private const int PhaseBeats = 4;

	/// <summary>
	/// A beat period also correlates at 2, 3 and 4 beats, half of it (the off-beat) only
	/// at every other. Double the period scores as well; the prior decides between them.
	/// </summary>
	private const int Multiples = 4;

	/// <summary>
	/// Below this the onsets are about as periodic as noise.
	/// </summary>
	private const double MinConfidence = 0.12;

	/// <summary>
	/// A new tempo more than this far from the current one has to show up in
	/// <see cref="SwitchEstimates"/> estimates in a row before it is taken.
	/// </summary>
	private const double SameTempo = 0.04;

	private const int SwitchEstimates = 3;
	private const double TempoSmoothing = 0.3;

	/// <summary>
	/// Below this RMS (about -50 dBFS) a microphone hears only the room.
	/// </summary>
	public const double MicrophoneSilence = 0.003;

	/// <summary>
	/// Loopback is digital silence when nothing plays, so even quiet music is above this
	/// (about -70 dBFS).
	/// </summary>
	public const double LoopbackSilence = 0.0003;

	private readonly int _sampleRate;
	private readonly double _silence;
	private readonly int _hop;
	private readonly double _frameRate;
	private readonly Fft _fft = new(FftSize);
	private readonly double[] _window = new double[FftSize];
	private readonly float[] _input = new float[FftSize];
	private readonly double[] _re = new double[FftSize];
	private readonly double[] _im = new double[FftSize];
	private readonly double[] _previous;
	private readonly int _bassBins;
	private readonly double _bassWeight;
	private readonly double[] _onsets;
	private readonly double[] _rms;
	private readonly double[] _series;
	private readonly int _minLag;
	private readonly int _maxLag;
	private readonly double[] _autocorrelation;

	private int _filled;
	private long _frames;
	private long _samples;
	private int _framesToEstimate;
	private double? _bpm;
	private double _candidate;
	private int _candidateCount;
	private double _peak;
	private double _level;

	/// <param name="silence">RMS below which there is no music</param>
	public BeatDetector(int sampleRate, double silence = MicrophoneSilence)
	{
		_sampleRate = sampleRate;
		_silence = silence;
		_peak = silence;
		_hop = (int)Math.Round(sampleRate * HopSeconds);
		_frameRate = (double)sampleRate / _hop;

		for (var i = 0; i < FftSize; i++)
		{
			_window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / FftSize);
		}

		_previous = new double[Math.Min(FftSize / 2, (int)(MaxFrequency * FftSize / sampleRate))];
		_bassBins = Math.Max(2, (int)(BassFrequency * FftSize / sampleRate) + 1);
		_bassWeight = (double)(_previous.Length - _bassBins) / (_bassBins - 1);

		var history = (int)(HistorySeconds * _frameRate);
		_onsets = new double[history];
		_rms = new double[history];
		_series = new double[history];

		_minLag = (int)Math.Floor(60 * _frameRate / MaxBpm);
		_maxLag = (int)Math.Ceiling(60 * _frameRate / MinBpm);
		_autocorrelation = new double[Multiples * _maxLag + 2];
	}

	/// <summary>
	/// Loudness relative to the recent peak, 0-1; 0 in silence.
	/// </summary>
	public double Level => _level;

	/// <summary>
	/// The tempo while there is a steady beat; null in silence or without a clear beat.
	/// </summary>
	public double? Bpm { get; private set; }

	/// <summary>
	/// Returns a new estimate when one was made from these samples and there is a beat.
	/// </summary>
	public BeatEstimate? Process(ReadOnlySpan<float> samples)
	{
		var beatSample = -1L;
		var confidence = 0.0;

		while (!samples.IsEmpty)
		{
			var take = Math.Min(samples.Length, _hop - _filled);
			samples[..take].CopyTo(_input.AsSpan(FftSize - _hop + _filled));
			samples = samples[take..];
			_filled += take;
			_samples += take;
			if (_filled < _hop)
			{
				break;
			}

			AnalyseFrame();
			_input.AsSpan(_hop).CopyTo(_input);
			_filled = 0;

			if (--_framesToEstimate <= 0)
			{
				_framesToEstimate = (int)(EstimateSeconds * _frameRate);
				(beatSample, confidence) = Estimate();
			}
		}

		return beatSample >= 0 && Bpm is { } bpm
			? new BeatEstimate(bpm, TimeSpan.FromSeconds((double)(_samples - beatSample) / _sampleRate), confidence)
			: null;
	}

	private void AnalyseFrame()
	{
		for (var i = 0; i < FftSize; i++)
		{
			_re[i] = _input[i] * _window[i];
			_im[i] = 0;
		}
		_fft.Transform(_re, _im);

		var flux = 0.0;
		for (var k = 1; k < _previous.Length; k++)
		{
			var magnitude = Math.Log(1 + 1000 * Math.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]) / (FftSize / 2));
			flux += Math.Max(0, magnitude - _previous[k]) * (k < _bassBins ? _bassWeight : 1);
			_previous[k] = magnitude;
		}

		var energy = 0.0;
		for (var i = FftSize - _hop; i < FftSize; i++)
		{
			energy += _input[i] * _input[i];
		}
		var rms = Math.Sqrt(energy / _hop);

		var slot = (int)(_frames % _onsets.Length);
		_onsets[slot] = flux;
		_rms[slot] = rms;
		_frames++;

		UpdateLevel(rms);
	}

	/// <summary>
	/// Follows the peak down over about 10 s, so the level adapts to how loud the music
	/// is at the microphone.
	/// </summary>
	private void UpdateLevel(double rms)
	{
		_peak = Math.Max(Math.Max(rms, _silence), _peak * Math.Pow(0.5, 1 / (10 * _frameRate)));
		var target = rms < _silence ? 0 : rms / _peak;
		var rate = target > _level ? 0.6 : 1 / (0.15 * _frameRate);
		_level += (target - _level) * rate;
	}

	/// <returns>The sample position of the last beat, or -1 when there is no beat</returns>
	private (long BeatSample, double Confidence) Estimate()
	{
		var n = (int)Math.Min(_frames, _onsets.Length);
		if (n < MinHistorySeconds * _frameRate || n < 2 * _maxLag || IsSilent(n))
		{
			Bpm = null;
			return (-1, 0);
		}

		// Chronological, without the mean, so the autocorrelation doesn't fall off with the lag
		var series = _series.AsSpan(0, n);
		var first = _frames - n;
		var mean = 0.0;
		for (var i = 0; i < n; i++)
		{
			series[i] = _onsets[(int)((first + i) % _onsets.Length)];
			mean += series[i];
		}
		mean /= n;
		for (var i = 0; i < n; i++)
		{
			series[i] -= mean;
		}

		var maxLag = Math.Min(_autocorrelation.Length - 1, n - 1);
		for (var lag = 0; lag <= maxLag; lag++)
		{
			var sum = 0.0;
			for (var i = lag; i < n; i++)
			{
				sum += series[i] * series[i - lag];
			}
			_autocorrelation[lag] = sum / (n - lag);
		}
		if (_autocorrelation[0] <= 0)
		{
			Bpm = null;
			return (-1, 0);
		}

		var bestLag = 0;
		var bestScore = double.MinValue;
		for (var lag = _minLag; lag <= _maxLag; lag++)
		{
			var score = Periodicity(lag, maxLag) * Prior(lag);
			if (score > bestScore)
			{
				bestScore = score;
				bestLag = lag;
			}
		}

		var confidence = Math.Clamp(_autocorrelation[bestLag] / _autocorrelation[0], 0, 1);
		if (confidence < MinConfidence)
		{
			Bpm = null;
			return (-1, confidence);
		}

		var bpm = UpdateTempo(60 * _frameRate / RefineLag(bestLag));
		if (bpm is null)
		{
			return (-1, confidence);
		}
		Bpm = bpm;

		var beatFrame = _frames - 1 - BestPhase(series, 60 * _frameRate / bpm.Value);
		// The flux of a frame peaks when the onset is about in the middle of the window
		return ((beatFrame + 1) * _hop - FftSize / 2, confidence);
	}

	private bool IsSilent(int n)
	{
		var recent = Math.Min(n, (int)(2 * _frameRate));
		var sum = 0.0;
		for (var i = 1; i <= recent; i++)
		{
			var rms = _rms[(int)((_frames - i) % _rms.Length)];
			sum += rms * rms;
		}
		return Math.Sqrt(sum / recent) < _silence;
	}

	private double Periodicity(int lag, int maxLag)
	{
		var sum = 0.0;
		for (var m = 1; m <= Multiples && m * lag <= maxLag; m++)
		{
			var center = m * lag;
			var value = _autocorrelation[center];
			if (m > 1)
			{
				// The lag is rounded to a frame, and the error grows with each multiple
				value = Math.Max(value, Math.Max(_autocorrelation[center - 1], _autocorrelation[Math.Min(center + 1, maxLag)]));
			}
			sum += value;
		}
		return sum;
	}

	private double Prior(int lag)
	{
		var octaves = Math.Log2(60 * _frameRate / lag / PreferredBpm) / PriorOctaves;
		return Math.Exp(-0.5 * octaves * octaves);
	}

	/// <summary>
	/// The peak between lags, from a parabola through the neighbours.
	/// </summary>
	private double RefineLag(int lag)
	{
		var a = _autocorrelation[lag - 1];
		var b = _autocorrelation[lag];
		var c = _autocorrelation[lag + 1];
		var denominator = a - 2 * b + c;
		return denominator < 0 ? lag + Math.Clamp(0.5 * (a - c) / denominator, -0.5, 0.5) : lag;
	}

	/// <summary>
	/// Smooths small changes and holds back a jump to a different tempo until it has
	/// been seen a few times.
	/// </summary>
	private double? UpdateTempo(double bpm)
	{
		if (_bpm is not { } current)
		{
			_bpm = bpm;
			_candidateCount = 0;
			return null;
		}

		if (Math.Abs(bpm / current - 1) <= SameTempo)
		{
			_bpm = current + (bpm - current) * TempoSmoothing;
			_candidateCount = 0;
			return _bpm;
		}

		_candidateCount = Math.Abs(bpm / _candidate - 1) <= SameTempo ? _candidateCount + 1 : 1;
		_candidate = bpm;
		if (_candidateCount >= SwitchEstimates)
		{
			_bpm = bpm;
			_candidateCount = 0;
			return _bpm;
		}
		return Bpm is null ? null : current;
	}

	/// <returns>How many frames ago the last beat was</returns>
	private static int BestPhase(ReadOnlySpan<double> series, double period)
	{
		var last = series.Length - 1;
		var bestOffset = 0;
		var bestScore = double.MinValue;
		for (var offset = 0; offset < (int)Math.Ceiling(period); offset++)
		{
			var score = 0.0;
			for (var beat = 0; beat < PhaseBeats; beat++)
			{
				var index = last - offset - (int)Math.Round(beat * period);
				if (index >= 1)
				{
					// Tolerate a frame of jitter around each beat
					score += Math.Max(series[index], Math.Max(series[index - 1], series[Math.Min(index + 1, last)]));
				}
			}
			if (score > bestScore)
			{
				bestScore = score;
				bestOffset = offset;
			}
		}
		return bestOffset;
	}
}
