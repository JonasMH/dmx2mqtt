using Dmx2Mqtt.Effects;
using Dmx2Mqtt.Output;

namespace Dmx2Mqtt.Test;

public class EffectTests
{
	public static TheoryData<string> Names => [.. EffectFactory.Names];

	[Theory]
	[MemberData(nameof(Names))]
	public void RendersTenSecondsAndLightsSomething(string name)
	{
		var engine = new EffectEngine(new Random(1));
		var pixels = new Rgb[16];
		var lit = false;

		for (var frame = 0; frame < 400; frame++)
		{
			engine.Render(pixels, name, new Rgb(255, 255, 255), frame / 40.0, 1 / 40.0);
			lit |= pixels.Any(x => x != Rgb.Black);
		}

		Assert.True(lit, $"{name} never lit a pixel");
	}

	[Fact]
	public void SolidUsesTheColor()
	{
		var pixels = new Rgb[3];

		new EffectEngine().Render(pixels, "Solid", new Rgb(10, 20, 30), 0, 0);

		Assert.All(pixels, x => Assert.Equal(new Rgb(10, 20, 30), x));
	}

	[Fact]
	public void UnknownEffectFallsBackToSolid()
	{
		Assert.IsType<SolidEffect>(EffectFactory.Create("Nope"));
	}

	[Fact]
	public unsafe void Termios2MatchesTheKernelLayout()
	{
		Assert.Equal(44, sizeof(Termios2));
	}
}
