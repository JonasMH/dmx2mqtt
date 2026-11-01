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
	public IDmxOutput Create()
	{
		var device = options.Value.Device;
		if (string.IsNullOrEmpty(device))
		{
			return new NullDmxOutput(loggerFactory.CreateLogger<NullDmxOutput>());
		}
		ISerialPort port = OperatingSystem.IsWindows() ? new WindowsSerialPort(device) : new LinuxSerialPort(device);
		return new OpenDmxOutput(device, port, loggerFactory.CreateLogger<OpenDmxOutput>());
	}
}

internal static class PreciseWait
{
	// Windows sleeps in timer ticks, so even at 1 ms resolution a sleep can end a tick late
	private static readonly TimeSpan SleepMargin = TimeSpan.FromMilliseconds(OperatingSystem.IsWindows() ? 2 : 1);

	static PreciseWait()
	{
		if (OperatingSystem.IsWindows())
		{
			// The default 15.6 ms tick would make the frame rate stutter; per process
			// since Windows 10 2004, and reset when the process exits
			Win32.TimeBeginPeriod(1);
		}
	}

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
			if (remaining > SleepMargin + TimeSpan.FromMilliseconds(1))
			{
				Thread.Sleep(remaining - SleepMargin);
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
