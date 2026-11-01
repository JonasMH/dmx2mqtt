using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dmx2Mqtt.Output;

/// <summary>
/// FTDI based "Open DMX" interfaces (e.g. Eurolite USB-DMX512 Interface/Update Adapter).
/// The FTDI chip is a plain UART, so the host generates the DMX timing: a break,
/// mark after break, then start code 0 and the channels at 250 kbaud 8N2.
/// Uses the kernel's ftdi_sio driver (/dev/ttyUSB*), no libftdi needed.
/// </summary>
public sealed class OpenDmxOutput(string device, ILogger<OpenDmxOutput> logger) : IDmxOutput
{
	private const uint BaudRate = 250_000;
	private const int BitsPerSlot = 11; // start bit, 8 data bits, 2 stop bits

	// DMX512 minimums are 88 µs break and 8 µs mark after break
	private static readonly TimeSpan Break = TimeSpan.FromMicroseconds(150);
	private static readonly TimeSpan MarkAfterBreak = TimeSpan.FromMicroseconds(20);

	// tcdrain returns once the data has left the kernel, but the FTDI chip may still be
	// shifting out its FIFO. Allow for USB latency before the next break.
	private static readonly TimeSpan UsbMargin = TimeSpan.FromMilliseconds(3);

	private int _fd = -1;
	private byte[] _frame = [];

	public void Open()
	{
		_fd = LibC.Open(device, LibC.O_RDWR | LibC.O_NOCTTY | LibC.O_CLOEXEC);
		if (_fd < 0)
		{
			throw new IOException($"Cannot open {device}: {Marshal.GetLastPInvokeErrorMessage()}");
		}

		var tio = default(Termios2);
		Check(LibC.Ioctl(_fd, LibC.TCGETS2, ref tio), "TCGETS2");
		tio.IFlag = 0;
		tio.OFlag = 0;
		tio.LFlag = 0;
		tio.CFlag &= ~(LibC.CBAUD | (LibC.CBAUD << LibC.IBSHIFT) | LibC.CSIZE | LibC.PARENB | LibC.CRTSCTS);
		tio.CFlag |= LibC.BOTHER | (LibC.BOTHER << LibC.IBSHIFT) | LibC.CS8 | LibC.CSTOPB | LibC.CLOCAL | LibC.CREAD;
		tio.ISpeed = BaudRate;
		tio.OSpeed = BaudRate;
		Check(LibC.Ioctl(_fd, LibC.TCSETS2, ref tio), "TCSETS2");

		Check(LibC.Ioctl(_fd, LibC.TCGETS2, ref tio), "TCGETS2");
		if (tio.OSpeed != BaudRate)
		{
			throw new IOException($"{device} does not support {BaudRate} baud (got {tio.OSpeed})");
		}

		logger.LogInformation("Open DMX on {device}", device);
	}

	public unsafe void Send(ReadOnlySpan<byte> channels)
	{
		if (_frame.Length != channels.Length + 1)
		{
			_frame = new byte[channels.Length + 1];
		}
		_frame[0] = 0; // start code
		channels.CopyTo(_frame.AsSpan(1));

		Check(LibC.Ioctl(_fd, LibC.TIOCSBRK, 0), "TIOCSBRK");
		PreciseWait.For(Break);
		Check(LibC.Ioctl(_fd, LibC.TIOCCBRK, 0), "TIOCCBRK");
		PreciseWait.For(MarkAfterBreak);

		var writeStart = Stopwatch.GetTimestamp();
		fixed (byte* p = _frame)
		{
			var offset = 0;
			while (offset < _frame.Length)
			{
				var n = LibC.Write(_fd, p + offset, (nuint)(_frame.Length - offset));
				if (n < 0)
				{
					if (Marshal.GetLastPInvokeError() == LibC.EINTR)
					{
						continue;
					}
					throw new IOException($"Write to {device} failed: {Marshal.GetLastPInvokeErrorMessage()}");
				}
				offset += (int)n;
			}
		}
		Check(LibC.TcDrain(_fd), "tcdrain");

		var frameTime = TimeSpan.FromSeconds((double)_frame.Length * BitsPerSlot / BaudRate) + UsbMargin;
		PreciseWait.Until(writeStart + PreciseWait.Ticks(frameTime));
	}

	public void Dispose()
	{
		if (_fd >= 0)
		{
			LibC.Close(_fd);
			_fd = -1;
		}
	}

	private void Check(int result, string call)
	{
		if (result < 0)
		{
			throw new IOException($"{call} on {device} failed: {Marshal.GetLastPInvokeErrorMessage()}");
		}
	}
}
