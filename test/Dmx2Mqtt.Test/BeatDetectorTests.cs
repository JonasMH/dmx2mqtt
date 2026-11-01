using Dmx2Mqtt.Audio;

namespace Dmx2Mqtt.Test;

public class BeatDetectorTests
{
	private const int SampleRate = 22050;

	/// <summary>
	/// Like a capture callback every 20 ms.
	/// </summary>
	private const int Chunk = 441;

	[Theory]
	[InlineData(90)]
	[InlineData(120)]
	[InlineData(128)]
	[InlineData(140)]
	[InlineData(160)]
	public void FindsTheTempoAndTheBeatOfADrumLoop(double bpm)
	{
		var audio = DrumLoop(bpm, seconds: 12, firstBeat: 0.137, new Random(1));
		var detector = new BeatDetector(SampleRate);

		var estimate = Run(detector, audio);

		Assert.NotNull(estimate);
		Assert.Equal(bpm, estimate.Value.Bpm, 1.5);
		var period = 60 / bpm;
		var expected = (12 - 0.137) % period;
		Assert.InRange(PhaseError(estimate.Value.SinceBeat.TotalSeconds, expected, period), -0.02, 0.02);
	}

	[Fact]
	public void TakesFastTempoAsHalfTime()
	{
		var detector = new BeatDetector(SampleRate);

		var estimate = Run(detector, DrumLoop(174, seconds: 12, firstBeat: 0, new Random(1)));

		Assert.NotNull(estimate);
		Assert.Equal(87, estimate.Value.Bpm, 1.5);
	}

	[Fact]
	public void FollowsATempoChange()
	{
		var random = new Random(2);
		var detector = new BeatDetector(SampleRate);

		Run(detector, DrumLoop(100, seconds: 10, firstBeat: 0, random));
		var estimate = Run(detector, DrumLoop(130, seconds: 10, firstBeat: 0, random));

		Assert.NotNull(estimate);
		Assert.Equal(130, estimate.Value.Bpm, 1.5);
	}

	[Fact]
	public void NoBeatInSilence()
	{
		var detector = new BeatDetector(SampleRate);

		var estimate = Run(detector, new float[SampleRate * 10]);

		Assert.Null(estimate);
		Assert.Null(detector.Bpm);
		Assert.Equal(0, detector.Level);
	}

	[Fact]
	public void NoBeatInNoise()
	{
		var random = new Random(3);
		var audio = new float[SampleRate * 10];
		for (var i = 0; i < audio.Length; i++)
		{
			audio[i] = (float)(0.2 * (random.NextDouble() * 2 - 1));
		}
		var detector = new BeatDetector(SampleRate);

		Run(detector, audio);

		Assert.Null(detector.Bpm);
		Assert.True(detector.Level > 0.5);
	}

	private static BeatEstimate? Run(BeatDetector detector, float[] audio)
	{
		BeatEstimate? last = null;
		for (var i = 0; i < audio.Length; i += Chunk)
		{
			last = detector.Process(audio.AsSpan(i, Math.Min(Chunk, audio.Length - i))) ?? last;
		}
		return detector.Bpm is null ? null : last;
	}

	private static double PhaseError(double actual, double expected, double period)
	{
		var error = (actual - expected) / period;
		return (error - Math.Round(error)) * period;
	}

	/// <summary>
	/// Kick on every beat, snare on 2 and 4, hi-hat on the off-beats, over a quiet
	/// noise floor.
	/// </summary>
	private static float[] DrumLoop(double bpm, double seconds, double firstBeat, Random random)
	{
		var audio = new float[(int)(seconds * SampleRate)];
		for (var i = 0; i < audio.Length; i++)
		{
			audio[i] = (float)(0.01 * (random.NextDouble() * 2 - 1));
		}

		var period = 60 / bpm;
		var beat = 0;
		for (var t = firstBeat; t < seconds; t += period, beat++)
		{
			Add(audio, t, 0.15, x => 0.6 * Math.Sin(2 * Math.PI * (50 + 80 * Math.Exp(-x * 40)) * x) * Math.Exp(-x * 20));
			if (beat % 2 == 1)
			{
				Add(audio, t, 0.12, x => 0.3 * (random.NextDouble() * 2 - 1) * Math.Exp(-x * 30));
			}
			Add(audio, t + period / 2, 0.04, x => 0.15 * (random.NextDouble() * 2 - 1) * Math.Exp(-x * 100));
		}
		return audio;
	}

	private static void Add(float[] audio, double start, double length, Func<double, double> sound)
	{
		var first = (int)(start * SampleRate);
		for (var i = 0; i < length * SampleRate && first + i < audio.Length; i++)
		{
			audio[first + i] += (float)sound((double)i / SampleRate);
		}
	}
}
