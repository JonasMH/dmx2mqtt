namespace Dmx2Mqtt.Effects;

/// <summary>
/// Auto party mode: a new beat pattern every 16 beats, a new pair of colours every 32,
/// and a quarter-beat strobe fill on the last beat before each change. With a
/// microphone it gets brighter with the music and drifts slowly while it is quiet.
/// Ignores the chosen colour.
/// </summary>
public sealed class PartyEffect : IEffect
{
	private const int PatternBeats = 16;
	private const int PaletteBeats = 32;
	private const double QuietLevel = 0.05;
	private const double QuietSeconds = 2;

	private delegate void Pattern(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b);

	private static readonly Pattern[] Patterns = [Pulse, Alternate, Halves, Sweep, CenterOut, Sparkle];

	private long _section = long.MinValue;
	private long _paletteSection = long.MinValue;
	private int _pattern = -1;
	private double _hue;
	private double _quiet;

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		var beat = (long)Math.Floor(ctx.Beats);
		var phase = ctx.Beats - beat;

		var section = Math.DivRem(beat, PatternBeats, out var inSection);
		if (inSection < 0)
		{
			section--;
			inSection += PatternBeats;
		}
		if (section != _section)
		{
			_section = section;
			_pattern = (_pattern + 1 + ctx.Random.Next(Patterns.Length - 1)) % Patterns.Length;
		}

		var paletteSection = (long)Math.Floor(ctx.Beats / PaletteBeats);
		if (paletteSection != _paletteSection)
		{
			_paletteSection = paletteSection;
			_hue = (_hue + 0.2 + 0.6 * ctx.Random.NextDouble()) % 1;
		}

		_quiet = ctx.Level < QuietLevel ? _quiet + ctx.DeltaTime : 0;
		if (_quiet > QuietSeconds)
		{
			Ambient(pixels, ctx.Time, _hue);
			return;
		}

		var a = Rgb.FromHsv(_hue, 1, 1);
		var b = Rgb.FromHsv(_hue + 0.4, 1, 1);
		if (inSection == PatternBeats - 1)
		{
			pixels.Fill(phase * 4 % 1 < 0.35 ? a : Rgb.Black);
		}
		else
		{
			Patterns[_pattern](pixels, beat, phase, a, b);
		}

		if (ctx.Level is { } level)
		{
			var scale = 0.6 + 0.4 * Math.Min(1, level);
			for (var i = 0; i < pixels.Length; i++)
			{
				pixels[i] = pixels[i].Scale(scale);
			}
		}
	}

	private static double Decay(double phase) => (1 - phase) * (1 - phase);

	/// <summary>
	/// For the moving patterns: full on the beat, half by the end of it.
	/// </summary>
	private static double Accent(double phase) => 0.5 + 0.5 * Decay(phase);

	/// <summary>
	/// Everything flashes, alternating the two colours.
	/// </summary>
	private static void Pulse(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b) =>
		pixels.Fill((beat % 2 == 0 ? a : b).Scale(Decay(phase)));

	/// <summary>
	/// Odd and even pixels take turns.
	/// </summary>
	private static void Alternate(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b)
	{
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = i % 2 == beat % 2 ? a.Scale(Decay(phase)) : b.Scale(0.15);
		}
	}

	/// <summary>
	/// The two halves of the strip (with two fixtures, one each) take turns.
	/// </summary>
	private static void Halves(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b)
	{
		var half = pixels.Length / 2;
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = (i < half) == (beat % 2 == 0) ? a.Scale(Decay(phase)) : Rgb.Black;
		}
	}

	/// <summary>
	/// A short tail runs across the strip once per beat, back and forth.
	/// </summary>
	private static void Sweep(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b)
	{
		var n = pixels.Length;
		var head = phase * (n + 2);
		for (var i = 0; i < n; i++)
		{
			var position = beat % 2 == 0 ? i : n - 1 - i;
			var behind = head - position;
			pixels[i] = behind is >= 0 and < 3 ? a.Scale((1 - behind / 3) * Accent(phase)) : b.Scale(0.1 * Accent(phase));
		}
	}

	/// <summary>
	/// A ring grows from the middle of each half outwards on every beat.
	/// </summary>
	private static void CenterOut(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b)
	{
		var half = Math.Max(1, pixels.Length / 2);
		for (var i = 0; i < pixels.Length; i++)
		{
			var local = i % half;
			var distance = Math.Abs(local - (half - 1) / 2.0) / (half / 2.0);
			var ring = Math.Max(0, 1 - Math.Abs(distance - phase) * 3);
			pixels[i] = (beat % 2 == 0 ? a : b).Scale(ring * Accent(phase));
		}
	}

	/// <summary>
	/// A different random-looking set of pixels on every beat.
	/// </summary>
	private static void Sparkle(Span<Rgb> pixels, long beat, double phase, Rgb a, Rgb b)
	{
		for (var i = 0; i < pixels.Length; i++)
		{
			var hash = (uint)(beat * 7919 + i * 104729) * 2654435761u;
			pixels[i] = hash >> 30 == 0 ? a.Scale(Decay(phase)) : b.Scale(0.08);
		}
	}

	/// <summary>
	/// While the music is quiet: the colours drifting slowly along the strip, dimmed.
	/// </summary>
	private static void Ambient(Span<Rgb> pixels, double time, double hue)
	{
		for (var i = 0; i < pixels.Length; i++)
		{
			var level = 0.25 + 0.1 * Math.Sin(time * 0.5 + i * 0.6);
			pixels[i] = Rgb.FromHsv(hue + 0.15 * Math.Sin(time * 0.1 + i * 0.3), 1, level);
		}
	}
}
