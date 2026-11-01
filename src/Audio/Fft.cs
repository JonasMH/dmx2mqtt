namespace Dmx2Mqtt.Audio;

/// <summary>
/// In-place radix-2 FFT for a fixed size.
/// </summary>
public sealed class Fft
{
	private readonly int _size;
	private readonly double[] _cos;
	private readonly double[] _sin;
	private readonly int[] _reverse;

	public Fft(int size)
	{
		if (size < 2 || (size & (size - 1)) != 0)
		{
			throw new ArgumentException("Size must be a power of two", nameof(size));
		}

		_size = size;
		_cos = new double[size / 2];
		_sin = new double[size / 2];
		for (var i = 0; i < size / 2; i++)
		{
			_cos[i] = Math.Cos(2 * Math.PI * i / size);
			_sin[i] = -Math.Sin(2 * Math.PI * i / size);
		}

		var bits = int.Log2(size);
		_reverse = new int[size];
		for (var i = 0; i < size; i++)
		{
			for (var bit = 0; bit < bits; bit++)
			{
				_reverse[i] |= (i >> bit & 1) << (bits - 1 - bit);
			}
		}
	}

	public void Transform(Span<double> re, Span<double> im)
	{
		for (var i = 0; i < _size; i++)
		{
			var j = _reverse[i];
			if (j > i)
			{
				(re[i], re[j]) = (re[j], re[i]);
				(im[i], im[j]) = (im[j], im[i]);
			}
		}

		for (var length = 2; length <= _size; length *= 2)
		{
			var half = length / 2;
			var step = _size / length;
			for (var start = 0; start < _size; start += length)
			{
				for (var k = 0; k < half; k++)
				{
					var wr = _cos[k * step];
					var wi = _sin[k * step];
					var a = start + k;
					var b = a + half;
					var tr = re[b] * wr - im[b] * wi;
					var ti = re[b] * wi + im[b] * wr;
					re[b] = re[a] - tr;
					im[b] = im[a] - ti;
					re[a] += tr;
					im[a] += ti;
				}
			}
		}
	}
}
