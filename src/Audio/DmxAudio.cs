using System.Runtime.InteropServices;

namespace Dmx2Mqtt.Audio;

/// <summary>
/// The microphone capture library in src/Native (miniaudio). Linked into the binary in
/// the Docker image, loaded from next to it otherwise.
/// </summary>
internal static unsafe partial class DmxAudio
{
	private const string Library = "dmxaudio";

	[LibraryImport(Library, EntryPoint = "dmx_audio_open", StringMarshalling = StringMarshalling.Utf8)]
	public static partial nint Open(string? device, uint sampleRate,
		delegate* unmanaged[Cdecl]<nint, float*, uint, void> callback, nint user, byte* error, int errorLength);

	[LibraryImport(Library, EntryPoint = "dmx_audio_name")]
	public static partial byte* Name(nint audio);

	[LibraryImport(Library, EntryPoint = "dmx_audio_is_started")]
	public static partial int IsStarted(nint audio);

	[LibraryImport(Library, EntryPoint = "dmx_audio_close")]
	public static partial void Close(nint audio);
}
