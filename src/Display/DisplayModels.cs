using System.Text.Json.Serialization;

namespace Dmx2Mqtt.Display;

public class DisplayLayout
{
	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("effects")]
	public required IReadOnlyList<string> Effects { get; init; }

	[JsonPropertyName("fixtures")]
	public required IReadOnlyList<DisplayFixture> Fixtures { get; init; }
}

public class DisplayFixture
{
	[JsonPropertyName("name")]
	public required string Name { get; init; }

	/// <summary>
	/// Index into the preview pixels for each segment, in the fixture's own order.
	/// </summary>
	[JsonPropertyName("pixels")]
	public required int[] Pixels { get; init; }
}

/// <summary>
/// Sent to the display whenever something changes.
/// </summary>
public class DisplayState
{
	[JsonPropertyName("light")]
	public required LightPayload Light { get; init; }

	[JsonPropertyName("bpm")]
	public double Bpm { get; init; }

	[JsonPropertyName("output_connected")]
	public bool OutputConnected { get; init; }

	/// <summary>
	/// Null when the microphone is disabled.
	/// </summary>
	[JsonPropertyName("audio")]
	public DisplayAudio? Audio { get; init; }
}

public class DisplayAudio
{
	/// <summary>
	/// The microphone; null while there is none.
	/// </summary>
	[JsonPropertyName("device")]
	public string? Device { get; init; }

	/// <summary>
	/// The beat follows the microphone rather than tap tempo.
	/// </summary>
	[JsonPropertyName("following")]
	public bool Following { get; init; }

	/// <summary>
	/// There is a steady beat in the music.
	/// </summary>
	[JsonPropertyName("locked")]
	public bool Locked { get; init; }
}

public class AudioCommand
{
	[JsonPropertyName("following")]
	public bool Following { get; set; }
}

public class TempoCommand
{
	[JsonPropertyName("bpm")]
	public double Bpm { get; set; }

	/// <summary>
	/// How long ago the last tap was when the command was sent.
	/// </summary>
	[JsonPropertyName("since_beat_ms")]
	public double SinceBeatMs { get; set; }
}
