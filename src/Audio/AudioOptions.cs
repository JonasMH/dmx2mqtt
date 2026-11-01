namespace Dmx2Mqtt.Audio;

public class AudioOptions
{
	public const string Section = "Audio";

	/// <summary>
	/// Follow the beat of the music at the microphone.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Part of the capture device's name, e.g. "USB". Empty uses the default device.
	/// </summary>
	public string? Device { get; set; }

	public int SampleRate { get; set; } = 22050;

	/// <summary>
	/// How much earlier than detected the lights put the beat, to make up for the
	/// capture buffer, the analysis window and the DMX frame.
	/// </summary>
	public double LatencyMs { get; set; } = 60;
}
