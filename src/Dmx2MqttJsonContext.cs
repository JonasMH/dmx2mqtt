using System.Text.Json.Serialization;

namespace Dmx2Mqtt;

/// <summary>
/// Home Assistant MQTT JSON light schema, used for both commands and state.
/// </summary>
public class LightPayload
{
	[JsonPropertyName("state")]
	public string? State { get; set; }

	[JsonPropertyName("brightness")]
	public int? Brightness { get; set; }

	[JsonPropertyName("color_mode")]
	public string? ColorMode { get; set; }

	[JsonPropertyName("color")]
	public LightColor? Color { get; set; }

	[JsonPropertyName("effect")]
	public string? Effect { get; set; }

	public static LightPayload FromSnapshot(LightSnapshot snapshot) => new()
	{
		State = snapshot.On ? "ON" : "OFF",
		Brightness = snapshot.Brightness,
		ColorMode = "rgb",
		Color = new LightColor { R = snapshot.Color.R, G = snapshot.Color.G, B = snapshot.Color.B },
		Effect = snapshot.Effect,
	};
}

public class LightColor
{
	[JsonPropertyName("r")]
	public int R { get; set; }

	[JsonPropertyName("g")]
	public int G { get; set; }

	[JsonPropertyName("b")]
	public int B { get; set; }
}

[JsonSerializable(typeof(LightPayload))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class Dmx2MqttJsonContext : JsonSerializerContext
{
}
