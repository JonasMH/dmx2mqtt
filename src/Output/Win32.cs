using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Dmx2Mqtt.Output;

/// <summary>
/// The Win32 communications API (the FTDI VCP driver's COM ports) and the timer
/// resolution, which <see cref="PreciseWait"/> needs below the default 15.6 ms.
/// </summary>
internal static partial class Win32
{
	public const uint GENERIC_READ = 0x80000000;
	public const uint GENERIC_WRITE = 0x40000000;
	public const uint OPEN_EXISTING = 3;

	// DCB bit fields
	public const uint fBinary = 0x1;
	public const uint fDtrControlEnable = 0x1 << 4;
	public const uint fRtsControlEnable = 0x1 << 12;

	public const byte NOPARITY = 0;
	public const byte TWOSTOPBITS = 2;

	[LibraryImport("kernel32", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	public static partial SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
		nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool GetCommState(SafeFileHandle file, ref Dcb dcb);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool SetCommState(SafeFileHandle file, ref Dcb dcb);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool SetCommTimeouts(SafeFileHandle file, ref CommTimeouts timeouts);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool SetCommBreak(SafeFileHandle file);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool ClearCommBreak(SafeFileHandle file);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static unsafe partial bool WriteFile(SafeFileHandle file, byte* buffer, uint count, out uint written, nint overlapped);

	[LibraryImport("kernel32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool FlushFileBuffers(SafeFileHandle file);

	[LibraryImport("winmm", EntryPoint = "timeBeginPeriod")]
	public static partial uint TimeBeginPeriod(uint periodMilliseconds);
}

/// <summary>
/// DCB from winbase.h (28 bytes); the bit fields are in <see cref="Flags"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Dcb
{
	public uint Length;
	public uint BaudRate;
	public uint Flags;
	public ushort Reserved;
	public ushort XonLim;
	public ushort XoffLim;
	public byte ByteSize;
	public byte Parity;
	public byte StopBits;
	public byte XonChar;
	public byte XoffChar;
	public byte ErrorChar;
	public byte EofChar;
	public byte EvtChar;
	public ushort Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CommTimeouts
{
	public uint ReadIntervalTimeout;
	public uint ReadTotalTimeoutMultiplier;
	public uint ReadTotalTimeoutConstant;
	public uint WriteTotalTimeoutMultiplier;
	public uint WriteTotalTimeoutConstant;
}
