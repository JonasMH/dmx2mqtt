using System.Diagnostics;
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
			engine.Render(pixels, name, new Rgb(255, 255, 255), frame / 40.0, 1 / 40.0, frame / 20.0);
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
	public void BeatPulseIsBrightestOnTheBeat()
	{
		var engine = new EffectEngine();
		var pixels = new Rgb[4];
		var white = new Rgb(255, 255, 255);

		engine.Render(pixels, "Beat Pulse", white, 0, 0, 3.0);
		var onBeat = pixels[0].R;
		engine.Render(pixels, "Beat Pulse", white, 0, 0, 3.5);
		var halfway = pixels[0].R;
		engine.Render(pixels, "Beat Pulse", white, 0, 0, 3.95);

		Assert.Equal(255, onBeat);
		Assert.InRange(halfway, 1, 254);
		Assert.True(pixels[0].R < halfway);
	}

	[Fact]
	public void BeatChaseStepsOneBlockPerBeat()
	{
		var engine = new EffectEngine();
		var pixels = new Rgb[6];
		var lit = (double beats) =>
		{
			engine.Render(pixels, "Beat Chase", new Rgb(255, 0, 0), 0, 0, beats);
			return Enumerable.Range(0, pixels.Length).Where(i => pixels[i] != Rgb.Black).ToArray();
		};

		Assert.Equal([0, 1], lit(0.2));
		Assert.Equal([2, 3], lit(1.2));
		Assert.Equal([4, 5], lit(2.9));
		Assert.Equal([0, 1], lit(3.0));
	}

	[Fact]
	public void BeatClockFollowsTheTempoFromTheLastBeat()
	{
		var clock = new BeatClock();
		var now = Stopwatch.GetTimestamp();
		var second = Stopwatch.Frequency;

		clock.Set(90, TimeSpan.FromSeconds(1), now);

		Assert.Equal(1.5, clock.Beats(now), 3);
		Assert.Equal(3.0, clock.Beats(now + second), 3);
	}

	[Fact]
	public void BeatClockLimitsTheTempo()
	{
		var clock = new BeatClock();

		clock.Set(1000, TimeSpan.Zero, Stopwatch.GetTimestamp());

		Assert.Equal(BeatClock.MaxBpm, clock.Bpm);
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
