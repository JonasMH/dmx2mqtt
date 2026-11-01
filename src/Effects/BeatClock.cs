using System.Diagnostics;

namespace Dmx2Mqtt.Effects;

/// <summary>
/// Tempo and phase for the beat effects. Set by tap tempo on the local display, or
/// followed from the microphone by the audio beat detector.
/// </summary>
public sealed class BeatClock
{
	public const double DefaultBpm = 120;
	public const double MinBpm = 40;
	public const double MaxBpm = 240;

	/// <summary>
	/// Share of the phase error corrected per <see cref="Follow"/>, so a single bad
	/// estimate doesn't make the beat jump.
	/// </summary>
	private const double PhaseGain = 0.5;

	private readonly Lock _lock = new();
	private double _bpm = DefaultBpm;
	private long _anchor = Stopwatch.GetTimestamp();
	private double _anchorBeats;

	public double Bpm
	{
		get
		{
			lock (_lock)
			{
				return _bpm;
			}
		}
	}

	/// <summary>
	/// Jumps to the tempo and phase, for tap tempo.
	/// </summary>
	/// <param name="sinceBeat">How long ago the last beat was</param>
	public void Set(double bpm, TimeSpan sinceBeat, long now)
	{
		lock (_lock)
		{
			_bpm = Math.Clamp(bpm, MinBpm, MaxBpm);
			_anchor = now - (long)(sinceBeat.TotalSeconds * Stopwatch.Frequency);
			_anchorBeats = 0;
		}
	}

	/// <summary>
	/// Moves towards the tempo and phase, for continuous estimates. The beat count keeps
	/// running and never goes back past a beat that has already been shown.
	/// </summary>
	/// <param name="sinceBeat">How long ago the last beat was</param>
	public void Follow(double bpm, TimeSpan sinceBeat, long now)
	{
		lock (_lock)
		{
			var current = BeatsLocked(now);
			_bpm = Math.Clamp(bpm, MinBpm, MaxBpm);

			var error = sinceBeat.TotalSeconds * _bpm / 60 - current;
			error -= Math.Round(error);
			_anchor = now;
			_anchorBeats = Math.Max(Math.Floor(current), current + error * PhaseGain);
		}
	}

	/// <summary>
	/// Beats since the tempo was set; the integer part changes on every beat.
	/// </summary>
	public double Beats(long now)
	{
		lock (_lock)
		{
			return BeatsLocked(now);
		}
	}

	private double BeatsLocked(long now) =>
		_anchorBeats + Stopwatch.GetElapsedTime(_anchor, now).TotalSeconds * _bpm / 60;
}
