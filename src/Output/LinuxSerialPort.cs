using System.Runtime.InteropServices;

namespace Dmx2Mqtt.Output;

/// <summary>
/// A tty through the kernel's ftdi_sio driver (/dev/ttyUSB*), no libftdi needed.
/// </summary>
public sealed class LinuxSerialPort(string device) : ISerialPort
{
	private int _fd = -1;

	public void Open(uint baudRate)
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
		tio.ISpeed = baudRate;
		tio.OSpeed = baudRate;
		Check(LibC.Ioctl(_fd, LibC.TCSETS2, ref tio), "TCSETS2");

		Check(LibC.Ioctl(_fd, LibC.TCGETS2, ref tio), "TCGETS2");
		if (tio.OSpeed != baudRate)
		{
			throw new IOException($"{device} does not support {baudRate} baud (got {tio.OSpeed})");
		}
	}

	public void SetBreak(bool on) =>
		Check(LibC.Ioctl(_fd, on ? LibC.TIOCSBRK : LibC.TIOCCBRK, 0), on ? "TIOCSBRK" : "TIOCCBRK");

	public unsafe void Write(ReadOnlySpan<byte> data)
	{
		fixed (byte* p = data)
		{
			var offset = 0;
			while (offset < data.Length)
			{
				var n = LibC.Write(_fd, p + offset, (nuint)(data.Length - offset));
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
	}

	public void Drain() => Check(LibC.TcDrain(_fd), "tcdrain");

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
