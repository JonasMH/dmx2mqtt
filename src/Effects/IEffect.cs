namespace Dmx2Mqtt.Effects;

/// <param name="Time">Seconds since the effect was selected</param>
/// <param name="DeltaTime">Seconds since the previous frame</param>
/// <param name="Color">The colour chosen in Home Assistant</param>
public readonly record struct EffectContext(double Time, double DeltaTime, Rgb Color, Random Random);

/// <summary>
/// Renders one frame. Effects may keep state between frames; a new instance is
/// created whenever the effect is selected.
/// </summary>
public interface IEffect
{
	void Render(Span<Rgb> pixels, in EffectContext ctx);
}

public static class EffectFactory
{
	public const string Default = "Solid";

	public static readonly IReadOnlyList<string> Names =
		["Solid", "Breathe", "Rainbow", "Chase", "Fire", "Candle", "Lightning", "Strobe", "Spooky"];

	public static IEffect Create(string name) => name switch
	{
		"Breathe" => new BreatheEffect(),
		"Rainbow" => new RainbowEffect(),
		"Chase" => new ChaseEffect(),
		"Fire" => new FireEffect(),
		"Candle" => new CandleEffect(),
		"Lightning" => new LightningEffect(),
		"Strobe" => new StrobeEffect(),
		"Spooky" => new SpookyEffect(),
		_ => new SolidEffect(),
	};
}

/// <summary>
/// Keeps the current effect instance and restarts it when the selection changes.
/// </summary>
public sealed class EffectEngine(Random? random = null)
{
	private readonly Random _random = random ?? new Random();
	private string? _name;
	private IEffect _effect = new SolidEffect();
	private double _start;

	public void Render(Span<Rgb> pixels, string effect, Rgb color, double now, double deltaTime)
	{
		if (effect != _name)
		{
			_name = effect;
			_effect = EffectFactory.Create(effect);
			_start = now;
		}
		_effect.Render(pixels, new EffectContext(now - _start, deltaTime, color, _random));
	}
}
