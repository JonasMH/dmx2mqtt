namespace Dmx2Mqtt.Audio;

public class AudioOptions
{
	public const string Section = "Audio";

	/// <summary>
	/// Follow the beat of the music.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Listen to what the PC plays (e.g. Spotify) instead of a microphone. The default on
	/// Windows, the only place it works.
	/// </summary>
	public bool Loopback { get; set; } = OperatingSystem.IsWindows();

	/// <summary>
	/// Part of the device's name, e.g. "USB": a microphone, or with
	/// <see cref="Loopback"/> an output. Empty uses the default device.
	/// </summary>
	public string? Device { get; set; }

	public int SampleRate { get; set; } = 22050;

	/// <summary>
	/// How much earlier than detected the lights put the beat, to make up for the
	/// capture buffer, the analysis window and the DMX frame. Negative delays the lights,
	/// for <see cref="Loopback"/> when the speakers play later than the PC sends the
	/// audio (Bluetooth speakers by 100-200 ms).
	/// </summary>
	public double LatencyMs { get; set; } = 60;
}
