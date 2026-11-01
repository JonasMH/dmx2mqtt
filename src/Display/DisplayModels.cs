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
