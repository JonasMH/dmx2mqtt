using Dmx2Mqtt.Effects;

namespace Dmx2Mqtt.Test;

public class FixtureMapTests
{
	private static DmxOptions Options(params FixtureOptions[] fixtures) => new() { Channels = 512, Fixtures = [.. fixtures] };

	[Fact]
	public void TwoBarsInTwentyFourChannelMode()
	{
		var map = new FixtureMap(Options(
			new FixtureOptions { Name = "Bar 1", Address = 1, Pixels = 8 },
			new FixtureOptions { Name = "Bar 2", Address = 25, Pixels = 8 }));
		var pixels = new Rgb[16];
		pixels[0] = new Rgb(1, 2, 3);
		pixels[8] = new Rgb(4, 5, 6);
		pixels[15] = new Rgb(7, 8, 9);
		var universe = new byte[512];

		map.Write(pixels, 1, universe);

		Assert.Equal(16, map.PixelCount);
		Assert.Equal([1, 2, 3], universe[0..3]);
		Assert.Equal([4, 5, 6], universe[24..27]);
		Assert.Equal([7, 8, 9], universe[45..48]);
		Assert.All(universe[48..], x => Assert.Equal(0, x));
	}

	[Fact]
	public void ColorOrderReverseAndFixedChannels()
	{
		var map = new FixtureMap(Options(new FixtureOptions
		{
			Address = 10,
			Pixels = 2,
			ColorOrder = "GRB",
			PixelOffset = 1,
			Reverse = true,
			FixedChannels = [new FixedChannelOptions { Offset = 0, Value = 255 }],
		}));
		var universe = new byte[512];

		map.Write([new Rgb(1, 2, 3), new Rgb(4, 5, 6)], 1, universe);

		Assert.Equal(255, universe[9]);
		Assert.Equal([5, 4, 6, 2, 1, 3], universe[10..16]);
	}

	[Fact]
	public void RgbwTakesTheCommonPartAsWhite()
	{
		var map = new FixtureMap(Options(new FixtureOptions { Address = 1, ColorOrder = "RGBW" }));
		var universe = new byte[512];

		map.Write([new Rgb(200, 150, 100)], 1, universe);

		Assert.Equal([100, 50, 0, 100], universe[0..4]);
	}

	[Fact]
	public void ScalesBrightness()
	{
		var map = new FixtureMap(Options(new FixtureOptions { Address = 1 }));
		var universe = new byte[512];

		map.Write([new Rgb(255, 100, 0)], 0.5, universe);

		Assert.Equal([128, 50, 0], universe[0..3]);
	}

	[Fact]
	public void RejectsFixturesOutsideTheUniverse()
	{
		Assert.Throws<InvalidOperationException>(() =>
			new FixtureMap(Options(new FixtureOptions { Address = 500, Pixels = 8 })));
	}

	[Fact]
	public void RejectsInvalidColorOrder()
	{
		Assert.Throws<InvalidOperationException>(() =>
			new FixtureMap(Options(new FixtureOptions { ColorOrder = "RRB" })));
	}
}
