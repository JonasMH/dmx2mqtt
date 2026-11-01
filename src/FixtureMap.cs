using Dmx2Mqtt.Effects;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt;

/// <summary>
/// Maps the effect pixels to DMX channels of the configured fixtures.
/// </summary>
public sealed class FixtureMap
{
	private readonly record struct PixelSlot(int Channel, string Order);

	private readonly PixelSlot[] _pixels;
	private readonly (int Channel, byte Value)[] _fixed;

	public int PixelCount => _pixels.Length;

	public FixtureMap(IOptions<DmxOptions> options) : this(options.Value)
	{
	}

	public FixtureMap(DmxOptions options)
	{
		var pixels = new List<PixelSlot>();
		var fixedChannels = new List<(int, byte)>();

		foreach (var fixture in options.Fixtures)
		{
			var order = fixture.ColorOrder.ToUpperInvariant();
			if (order.Length == 0 || order.Any(c => !"RGBW".Contains(c)) || order.Distinct().Count() != order.Length)
			{
				throw new InvalidOperationException($"Fixture '{fixture.Name}': ColorOrder '{fixture.ColorOrder}' must use R, G, B and W once each");
			}

			var first = fixture.Address - 1 + fixture.PixelOffset;
			var last = first + fixture.Pixels * order.Length - 1;
			if (fixture.Address < 1 || last >= options.Channels)
			{
				throw new InvalidOperationException($"Fixture '{fixture.Name}': channels {first + 1}-{last + 1} are outside 1-{options.Channels}");
			}

			for (var i = 0; i < fixture.Pixels; i++)
			{
				var index = fixture.Reverse ? fixture.Pixels - 1 - i : i;
				pixels.Add(new PixelSlot(first + index * order.Length, order));
			}

			foreach (var ch in fixture.FixedChannels)
			{
				var channel = fixture.Address - 1 + ch.Offset;
				if (channel < 0 || channel >= options.Channels)
				{
					throw new InvalidOperationException($"Fixture '{fixture.Name}': fixed channel {channel + 1} is outside 1-{options.Channels}");
				}
				fixedChannels.Add((channel, ch.Value));
			}
		}

		_pixels = [.. pixels];
		_fixed = [.. fixedChannels];
	}

	/// <summary>
	/// Writes all fixtures into <paramref name="universe"/> (channel 1 at index 0).
	/// </summary>
	public void Write(ReadOnlySpan<Rgb> pixels, double scale, Span<byte> universe)
	{
		universe.Clear();

		foreach (var (channel, value) in _fixed)
		{
			universe[channel] = value;
		}

		for (var i = 0; i < _pixels.Length && i < pixels.Length; i++)
		{
			var slot = _pixels[i];
			var color = pixels[i].Scale(scale);
			int r = color.R, g = color.G, b = color.B, w = 0;

			if (slot.Order.Contains('W'))
			{
				w = Math.Min(r, Math.Min(g, b));
				r -= w;
				g -= w;
				b -= w;
			}

			for (var k = 0; k < slot.Order.Length; k++)
			{
				universe[slot.Channel + k] = (byte)(slot.Order[k] switch
				{
					'R' => r,
					'G' => g,
					'B' => b,
					_ => w,
				});
			}
		}
	}
}
