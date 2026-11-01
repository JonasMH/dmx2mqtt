using System.Diagnostics;

namespace Dmx2Mqtt.Effects;

/// <summary>
/// Tempo and phase for the beat effects. Set by tap tempo on the local display;
/// an audio beat detector could set it the same way.
/// </summary>
public sealed class BeatClock
{
	public const double DefaultBpm = 120;
	public const double MinBpm = 40;
	public const double MaxBpm = 240;

	private readonly Lock _lock = new();
	private double _bpm = DefaultBpm;
	private long _anchor = Stopwatch.GetTimestamp();

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

	/// <param name="sinceBeat">How long ago the last beat was</param>
	public void Set(double bpm, TimeSpan sinceBeat, long now)
	{
		lock (_lock)
		{
			_bpm = Math.Clamp(bpm, MinBpm, MaxBpm);
			_anchor = now - (long)(sinceBeat.TotalSeconds * Stopwatch.Frequency);
		}
	}

	/// <summary>
	/// Beats since the tempo was set; the integer part changes on every beat.
	/// </summary>
	public double Beats(long now)
	{
		lock (_lock)
		{
			return Stopwatch.GetElapsedTime(_anchor, now).TotalSeconds * _bpm / 60;
		}
	}
}
