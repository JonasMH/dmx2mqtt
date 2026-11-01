namespace Dmx2Mqtt.Effects;

public sealed class SolidEffect : IEffect
{
	public void Render(Span<Rgb> pixels, in EffectContext ctx) => pixels.Fill(ctx.Color);
}

/// <summary>
/// The chosen colour slowly fading up and down.
/// </summary>
public sealed class BreatheEffect : IEffect
{
	private const double Period = 4;

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		var level = 0.1 + 0.9 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * ctx.Time / Period));
		pixels.Fill(ctx.Color.Scale(level));
	}
}

public sealed class RainbowEffect : IEffect
{
	private const double Period = 10;

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = Rgb.FromHsv(ctx.Time / Period + (double)i / pixels.Length, 1, 1);
		}
	}
}

/// <summary>
/// A segment in the chosen colour running along the strip with a fading tail.
/// </summary>
public sealed class ChaseEffect : IEffect
{
	private const double PixelsPerSecond = 6;
	private const double Tail = 3;

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		var n = pixels.Length;
		var head = ctx.Time * PixelsPerSecond % n;
		for (var i = 0; i < n; i++)
		{
			var behind = ((head - i) % n + n) % n;
			pixels[i] = ctx.Color.Scale(1 - behind / Tail);
		}
	}
}

/// <summary>
/// Each pixel flickers independently through a black-red-orange-yellow palette.
/// </summary>
public sealed class FireEffect : IEffect
{
	private static readonly Rgb Ember = new(180, 0, 0);
	private static readonly Rgb Flame = new(255, 90, 0);
	private static readonly Rgb Tip = new(255, 180, 40);

	private double[] _heat = [];
	private double[] _target = [];
	private double[] _nextChange = [];

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		if (_heat.Length != pixels.Length)
		{
			_heat = new double[pixels.Length];
			_target = new double[pixels.Length];
			_nextChange = new double[pixels.Length];
		}

		for (var i = 0; i < pixels.Length; i++)
		{
			if (ctx.Time >= _nextChange[i])
			{
				_target[i] = 0.35 + 0.65 * ctx.Random.NextDouble();
				_nextChange[i] = ctx.Time + 0.05 + 0.2 * ctx.Random.NextDouble();
			}
			_heat[i] += (_target[i] - _heat[i]) * Math.Min(1, ctx.DeltaTime * 12);
			pixels[i] = HeatColor(_heat[i]);
		}
	}

	private static Rgb HeatColor(double heat) => heat switch
	{
		< 0.4 => Rgb.Lerp(Rgb.Black, Ember, heat / 0.4),
		< 0.8 => Rgb.Lerp(Ember, Flame, (heat - 0.4) / 0.4),
		_ => Rgb.Lerp(Flame, Tip, (heat - 0.8) / 0.2),
	};
}

/// <summary>
/// Warm, softly flickering light with an occasional dip, like candles in a draught.
/// </summary>
public sealed class CandleEffect : IEffect
{
	private static readonly Rgb Warm = new(255, 110, 20);

	private double[] _level = [];
	private double[] _target = [];
	private double[] _nextChange = [];

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		if (_level.Length != pixels.Length)
		{
			_level = new double[pixels.Length];
			_target = new double[pixels.Length];
			_nextChange = new double[pixels.Length];
		}

		for (var i = 0; i < pixels.Length; i++)
		{
			if (ctx.Time >= _nextChange[i])
			{
				var dip = ctx.Random.NextDouble() < 0.05;
				_target[i] = dip ? 0.3 : 0.65 + 0.35 * ctx.Random.NextDouble();
				_nextChange[i] = ctx.Time + 0.1 + 0.3 * ctx.Random.NextDouble();
			}
			_level[i] += (_target[i] - _level[i]) * Math.Min(1, ctx.DeltaTime * 6);
			pixels[i] = Warm.Scale(_level[i]);
		}
	}
}

/// <summary>
/// Dark, with a burst of 2-4 short flashes in the chosen colour every 3-10 s,
/// on the whole strip or a part of it.
/// </summary>
public sealed class LightningEffect : IEffect
{
	private double _nextStrike = 1;
	private int _flashesLeft;
	private bool _on;
	private double _flashEnd;
	private double _gapEnd;
	private double _intensity;
	private int _start;
	private int _length;

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		var t = ctx.Time;
		var rnd = ctx.Random;
		pixels.Fill(Rgb.Black);

		if (_flashesLeft == 0 && t >= _nextStrike)
		{
			_flashesLeft = 2 + rnd.Next(3);
			_gapEnd = t;
			if (rnd.NextDouble() < 0.5 || pixels.Length < 3)
			{
				_start = 0;
				_length = pixels.Length;
			}
			else
			{
				_length = rnd.Next(pixels.Length / 3, pixels.Length + 1);
				_start = rnd.Next(0, pixels.Length - _length + 1);
			}
		}

		if (_flashesLeft > 0)
		{
			if (!_on && t >= _gapEnd)
			{
				_on = true;
				_flashEnd = t + 0.04 + 0.08 * rnd.NextDouble();
				_intensity = 0.5 + 0.5 * rnd.NextDouble();
			}
			else if (_on && t >= _flashEnd)
			{
				_on = false;
				_flashesLeft--;
				_gapEnd = t + 0.05 + 0.15 * rnd.NextDouble();
				if (_flashesLeft == 0)
				{
					_nextStrike = t + 3 + 7 * rnd.NextDouble();
				}
			}
		}

		if (_on)
		{
			pixels.Slice(_start, _length).Fill(ctx.Color.Scale(_intensity));
		}
	}
}

public sealed class StrobeEffect : IEffect
{
	private const double Period = 0.1;
	private const double OnTime = 0.03;

	public void Render(Span<Rgb> pixels, in EffectContext ctx) =>
		pixels.Fill(ctx.Time % Period < OnTime ? ctx.Color : Rgb.Black);
}

/// <summary>
/// Halloween: purple and green drifting along the strip, with pixels that
/// randomly drop out for a moment.
/// </summary>
public sealed class SpookyEffect : IEffect
{
	private static readonly Rgb Purple = new(140, 0, 255);
	private static readonly Rgb Green = new(40, 255, 20);

	private double[] _dropUntil = [];

	public void Render(Span<Rgb> pixels, in EffectContext ctx)
	{
		if (_dropUntil.Length != pixels.Length)
		{
			_dropUntil = new double[pixels.Length];
		}

		for (var i = 0; i < pixels.Length; i++)
		{
			if (ctx.Random.NextDouble() < 0.4 * ctx.DeltaTime)
			{
				_dropUntil[i] = ctx.Time + 0.05 + 0.25 * ctx.Random.NextDouble();
			}
			if (ctx.Time < _dropUntil[i])
			{
				pixels[i] = Rgb.Black;
				continue;
			}

			var mix = 0.5 + 0.5 * Math.Sin(ctx.Time * 0.25 + i * 0.9);
			var level = 0.55 + 0.45 * Math.Sin(ctx.Time * 0.7 + i * 1.7);
			pixels[i] = Rgb.Lerp(Purple, Green, mix).Scale(level);
		}
	}
}
