using Dmx2Mqtt.Effects;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt;

/// <param name="Pixels">Strip pixel index of each segment, in the fixture's own order</param>
public sealed record FixtureLayout(string Name, int[] Pixels);

/// <summary>
/// Maps the effect pixels to DMX channels of the configured fixtures.
/// </summary>
public sealed class FixtureMap
{
	private readonly record struct PixelSlot(int Channel, string Order);

	private readonly PixelSlot[] _pixels;
	private readonly (int Channel, byte Value)[] _fixed;

	public int PixelCount => _pixels.Length;

	/// <summary>
	/// The fixtures as they hang, for the live preview.
	/// </summary>
	public IReadOnlyList<FixtureLayout> Layout { get; }

	public FixtureMap(IOptions<DmxOptions> options) : this(options.Value)
	{
	}

	public FixtureMap(DmxOptions options)
	{
		var pixels = new List<PixelSlot>();
		var fixedChannels = new List<(int, byte)>();
		var layout = new List<FixtureLayout>();

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

			var segments = new int[fixture.Pixels];
			for (var i = 0; i < fixture.Pixels; i++)
			{
				var index = fixture.Reverse ? fixture.Pixels - 1 - i : i;
				segments[index] = pixels.Count;
				pixels.Add(new PixelSlot(first + index * order.Length, order));
			}
			layout.Add(new FixtureLayout(fixture.Name, segments));

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
		Layout = layout;
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
