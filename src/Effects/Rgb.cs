namespace Dmx2Mqtt.Effects;

public readonly record struct Rgb(byte R, byte G, byte B)
{
	public static readonly Rgb Black = new(0, 0, 0);

	public Rgb Scale(double factor)
	{
		factor = Math.Clamp(factor, 0, 1);
		return new((byte)(R * factor + 0.5), (byte)(G * factor + 0.5), (byte)(B * factor + 0.5));
	}

	public static Rgb Lerp(Rgb a, Rgb b, double t)
	{
		t = Math.Clamp(t, 0, 1);
		return new(
			(byte)(a.R + (b.R - a.R) * t + 0.5),
			(byte)(a.G + (b.G - a.G) * t + 0.5),
			(byte)(a.B + (b.B - a.B) * t + 0.5));
	}

	/// <summary>
	/// Hue, saturation and value all 0-1.
	/// </summary>
	public static Rgb FromHsv(double h, double s, double v)
	{
		h = (h % 1 + 1) % 1 * 6;
		var i = (int)h;
		var f = h - i;
		var p = v * (1 - s);
		var q = v * (1 - s * f);
		var t = v * (1 - s * (1 - f));
		var (r, g, b) = i switch
		{
			0 => (v, t, p),
			1 => (q, v, p),
			2 => (p, v, t),
			3 => (p, q, v),
			4 => (t, p, v),
			_ => (v, p, q),
		};
		return new((byte)(r * 255 + 0.5), (byte)(g * 255 + 0.5), (byte)(b * 255 + 0.5));
	}
}
