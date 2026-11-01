using Dmx2Mqtt.Effects;

namespace Dmx2Mqtt;

public readonly record struct LightSnapshot(bool On, byte Brightness, Rgb Color, string Effect);

/// <summary>
/// The light as Home Assistant sees it. Written by MQTT, read by the renderer every frame.
/// </summary>
public sealed class LightState
{
	private readonly Lock _lock = new();
	private LightSnapshot _current = new(false, 255, new Rgb(255, 140, 60), EffectFactory.Default);

	public LightSnapshot Snapshot()
	{
		lock (_lock)
		{
			return _current;
		}
	}

	/// <summary>
	/// Applies a Home Assistant JSON light command. Missing fields keep their value.
	/// </summary>
	public LightSnapshot Apply(LightPayload command)
	{
		lock (_lock)
		{
			var next = _current;

			if (command.State is { } state)
			{
				next = next with { On = state.Equals("ON", StringComparison.OrdinalIgnoreCase) };
			}
			if (command.Brightness is { } brightness)
			{
				next = brightness <= 0
					? next with { On = false }
					: next with { Brightness = (byte)Math.Min(brightness, 255) };
			}
			if (command.Color is { } color)
			{
				next = next with { Color = new Rgb(ToByte(color.R), ToByte(color.G), ToByte(color.B)) };
			}
			if (command.Effect is { } effect && EffectFactory.Names.Contains(effect))
			{
				next = next with { Effect = effect };
			}

			_current = next;
			return next;
		}
	}

	private static byte ToByte(int value) => (byte)Math.Clamp(value, 0, 255);
}
