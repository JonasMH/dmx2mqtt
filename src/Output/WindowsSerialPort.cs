using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Dmx2Mqtt.Output;

/// <summary>
/// A COM port through FTDI's VCP driver (e.g. COM3), no D2XX needed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSerialPort(string device) : ISerialPort
{
	// A write of 513 slots takes 23 ms; fail instead of hanging if the dongle is gone
	private const uint WriteTimeoutMilliseconds = 1000;

	private SafeFileHandle? _handle;

	public void Open(uint baudRate)
	{
		// COM10 and up only open as \\.\COM10
		var path = device.StartsWith(@"\\", StringComparison.Ordinal) ? device : @"\\.\" + device;
		_handle = Win32.CreateFile(path, Win32.GENERIC_READ | Win32.GENERIC_WRITE, 0, 0, Win32.OPEN_EXISTING, 0, 0);
		if (_handle.IsInvalid)
		{
			throw new IOException($"Cannot open {device}: {Marshal.GetLastPInvokeErrorMessage()}");
		}

		var dcb = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
		Check(Win32.GetCommState(_handle, ref dcb), "GetCommState");
		dcb.BaudRate = baudRate;
		// No flow control; DTR and RTS on, as a Linux tty has them after open
		dcb.Flags = Win32.fBinary | Win32.fDtrControlEnable | Win32.fRtsControlEnable;
		dcb.ByteSize = 8;
		dcb.Parity = Win32.NOPARITY;
		dcb.StopBits = Win32.TWOSTOPBITS;
		Check(Win32.SetCommState(_handle, ref dcb), "SetCommState");

		Check(Win32.GetCommState(_handle, ref dcb), "GetCommState");
		if (dcb.BaudRate != baudRate)
		{
			throw new IOException($"{device} does not support {baudRate} baud (got {dcb.BaudRate})");
		}

		var timeouts = new CommTimeouts { WriteTotalTimeoutConstant = WriteTimeoutMilliseconds };
		Check(Win32.SetCommTimeouts(_handle, ref timeouts), "SetCommTimeouts");
	}

	public void SetBreak(bool on) => Check(
		on ? Win32.SetCommBreak(Handle) : Win32.ClearCommBreak(Handle),
		on ? "SetCommBreak" : "ClearCommBreak");

	public unsafe void Write(ReadOnlySpan<byte> data)
	{
		fixed (byte* p = data)
		{
			Check(Win32.WriteFile(Handle, p, (uint)data.Length, out var written, 0), "WriteFile");
			if (written != data.Length)
			{
				throw new IOException($"Write to {device} timed out ({written} of {data.Length} bytes)");
			}
		}
	}

	// For a COM port this waits until the driver has sent its transmit buffer
	public void Drain() => Check(Win32.FlushFileBuffers(Handle), "FlushFileBuffers");

	public void Dispose()
	{
		_handle?.Dispose();
		_handle = null;
	}

	private SafeFileHandle Handle => _handle ?? throw new InvalidOperationException($"{device} is not open");

	private void Check(bool result, string call)
	{
		if (!result)
		{
			throw new IOException($"{call} on {device} failed: {Marshal.GetLastPInvokeErrorMessage()}");
		}
	}
}
