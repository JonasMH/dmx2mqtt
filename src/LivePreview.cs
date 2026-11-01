using Dmx2Mqtt.Effects;

namespace Dmx2Mqtt;

/// <summary>
/// The last frame as it went to the fixtures (after brightness and gamma), for the
/// local display.
/// </summary>
public sealed class LivePreview
{
	private readonly Lock _lock = new();
	private byte[] _rgb = [];

	public void Update(ReadOnlySpan<Rgb> pixels, double scale)
	{
		lock (_lock)
		{
			if (_rgb.Length != pixels.Length * 3)
			{
				_rgb = new byte[pixels.Length * 3];
			}
			for (var i = 0; i < pixels.Length; i++)
			{
				var color = pixels[i].Scale(scale);
				_rgb[i * 3] = color.R;
				_rgb[i * 3 + 1] = color.G;
				_rgb[i * 3 + 2] = color.B;
			}
		}
	}

	/// <summary>
	/// RRGGBB per strip pixel.
	/// </summary>
	public string ToHex()
	{
		lock (_lock)
		{
			return Convert.ToHexStringLower(_rgb);
		}
	}
}
