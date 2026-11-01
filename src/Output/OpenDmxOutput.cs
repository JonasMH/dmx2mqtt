using System.Diagnostics;

namespace Dmx2Mqtt.Output;

/// <summary>
/// FTDI based "Open DMX" interfaces (e.g. Eurolite USB-DMX512 Interface/Update Adapter).
/// The FTDI chip is a plain UART, so the host generates the DMX timing: a break,
/// mark after break, then start code 0 and the channels at 250 kbaud 8N2.
/// </summary>
public sealed class OpenDmxOutput(string device, ISerialPort port, ILogger<OpenDmxOutput> logger) : IDmxOutput
{
	private const uint BaudRate = 250_000;
	private const int BitsPerSlot = 11; // start bit, 8 data bits, 2 stop bits

	// DMX512 minimums are 88 µs break and 8 µs mark after break
	private static readonly TimeSpan Break = TimeSpan.FromMicroseconds(150);
	private static readonly TimeSpan MarkAfterBreak = TimeSpan.FromMicroseconds(20);

	// Drain returns once the data has left the driver, but the FTDI chip may still be
	// shifting out its FIFO. Allow for USB latency before the next break.
	private static readonly TimeSpan UsbMargin = TimeSpan.FromMilliseconds(3);

	private byte[] _frame = [];

	public void Open()
	{
		port.Open(BaudRate);
		logger.LogInformation("Open DMX on {device}", device);
	}

	public void Send(ReadOnlySpan<byte> channels)
	{
		if (_frame.Length != channels.Length + 1)
		{
			_frame = new byte[channels.Length + 1];
		}
		_frame[0] = 0; // start code
		channels.CopyTo(_frame.AsSpan(1));

		port.SetBreak(true);
		PreciseWait.For(Break);
		port.SetBreak(false);
		PreciseWait.For(MarkAfterBreak);

		var writeStart = Stopwatch.GetTimestamp();
		port.Write(_frame);
		port.Drain();

		var frameTime = TimeSpan.FromSeconds((double)_frame.Length * BitsPerSlot / BaudRate) + UsbMargin;
		PreciseWait.Until(writeStart + PreciseWait.Ticks(frameTime));
	}

	public void Dispose() => port.Dispose();
}
