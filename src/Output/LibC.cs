using System.Runtime.InteropServices;

namespace Dmx2Mqtt.Output;

/// <summary>
/// The Linux serial calls .NET's SerialPort does not offer: arbitrary baud rates
/// (termios2/BOTHER) and explicit break control.
/// </summary>
internal static partial class LibC
{
	public const int O_RDWR = 0x2;
	public const int O_NOCTTY = 0x100;
	public const int O_CLOEXEC = 0x80000;
	public const int EINTR = 4;

	// asm-generic ioctl numbers, the same on x86_64 and arm64
	public const nuint TCGETS2 = 0x802C542A;
	public const nuint TCSETS2 = 0x402C542B;
	public const nuint TIOCSBRK = 0x5427;
	public const nuint TIOCCBRK = 0x5428;

	public const uint CBAUD = 0x100F;
	public const uint BOTHER = 0x1000;
	public const int IBSHIFT = 16;
	public const uint CSIZE = 0x30;
	public const uint CS8 = 0x30;
	public const uint CSTOPB = 0x40;
	public const uint CREAD = 0x80;
	public const uint PARENB = 0x100;
	public const uint CLOCAL = 0x800;
	public const uint CRTSCTS = 0x80000000;

	[LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
	public static partial int Open(string path, int flags);

	[LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
	public static partial int Close(int fd);

	[LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
	public static unsafe partial nint Write(int fd, byte* buffer, nuint count);

	[LibraryImport("libc", EntryPoint = "tcdrain", SetLastError = true)]
	public static partial int TcDrain(int fd);

	[LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
	public static partial int Ioctl(int fd, nuint request, ref Termios2 arg);

	[LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
	public static partial int Ioctl(int fd, nuint request, nint arg);
}

/// <summary>
/// struct termios2 from asm-generic/termbits.h (44 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Termios2
{
	public uint IFlag;
	public uint OFlag;
	public uint CFlag;
	public uint LFlag;
	public byte Line;
	public fixed byte CC[19];
	public uint ISpeed;
	public uint OSpeed;
}
