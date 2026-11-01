using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt.Output;

public interface IDmxOutput : IDisposable
{
	void Open();

	/// <summary>
	/// Sends one DMX frame (channel 1 at index 0). Blocks until it is on the wire.
	/// </summary>
	void Send(ReadOnlySpan<byte> channels);
}

/// <summary>
/// Used when no device is configured, to run the service without hardware.
/// </summary>
public sealed class NullDmxOutput(ILogger<NullDmxOutput> logger) : IDmxOutput
{
	public void Open() => logger.LogWarning("No DMX device configured, running without output");

	public void Send(ReadOnlySpan<byte> channels)
	{
	}

	public void Dispose()
	{
	}
}

public class DmxOutputFactory(IOptions<DmxOptions> options, ILoggerFactory loggerFactory)
{
	public IDmxOutput Create() => string.IsNullOrEmpty(options.Value.Device)
		? new NullDmxOutput(loggerFactory.CreateLogger<NullDmxOutput>())
		: new OpenDmxOutput(options.Value.Device, loggerFactory.CreateLogger<OpenDmxOutput>());
}

internal static class PreciseWait
{
	/// <summary>
	/// Waits until the <see cref="Stopwatch"/> timestamp; sleeps for the bulk, spins the last part.
	/// </summary>
	public static void Until(long timestamp)
	{
		while (true)
		{
			var remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), timestamp);
			if (remaining <= TimeSpan.Zero)
			{
				return;
			}
			if (remaining > TimeSpan.FromMilliseconds(2))
			{
				Thread.Sleep(remaining - TimeSpan.FromMilliseconds(1));
			}
			else
			{
				Thread.SpinWait(20);
			}
		}
	}

	public static void For(TimeSpan duration) => Until(Stopwatch.GetTimestamp() + Ticks(duration));

	public static long Ticks(TimeSpan duration) => (long)(duration.TotalSeconds * Stopwatch.Frequency);
}
