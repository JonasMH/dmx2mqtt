namespace Dmx2Mqtt;

public class DmxOptions
{
	public const string Section = "Dmx";

	/// <summary>
	/// Serial device of the FTDI based Open DMX interface, e.g. /dev/ttyUSB0 or COM3 on Windows.
	/// Empty runs without output (dry run).
	/// </summary>
	public string? Device { get; set; }

	/// <summary>
	/// Device and entity name in Home Assistant.
	/// </summary>
	public string Name { get; set; } = "DMX";

	public int FrameRate { get; set; } = 40;

	/// <summary>
	/// Channels sent per frame. 512 is the safe choice, fewer gives a higher frame rate.
	/// </summary>
	public int Channels { get; set; } = 512;

	/// <summary>
	/// Fade time when turning on/off or changing brightness.
	/// </summary>
	public double FadeSeconds { get; set; } = 0.4;

	/// <summary>
	/// Applied to the brightness, so low brightness levels are usable.
	/// </summary>
	public double Gamma { get; set; } = 2.2;

	/// <summary>
	/// Fixtures in order; their pixels form one strip for the effects.
	/// </summary>
	public List<FixtureOptions> Fixtures { get; set; } = [];
}

public class FixtureOptions
{
	public string Name { get; set; } = "";

	/// <summary>
	/// DMX start address (1-based).
	/// </summary>
	public int Address { get; set; } = 1;

	public int Pixels { get; set; } = 1;

	/// <summary>
	/// Channel order of one pixel, using R, G, B and W. "RGBW" derives white from the
	/// common part of the colour.
	/// </summary>
	public string ColorOrder { get; set; } = "RGB";

	/// <summary>
	/// Offset of the first pixel channel from the start address, e.g. when the fixture
	/// has a master dimmer first.
	/// </summary>
	public int PixelOffset { get; set; }

	public bool Reverse { get; set; }

	/// <summary>
	/// Channels with a constant value, e.g. a master dimmer at 255.
	/// </summary>
	public List<FixedChannelOptions> FixedChannels { get; set; } = [];
}

public class FixedChannelOptions
{
	/// <summary>
	/// Offset from the fixture's start address (0 is the start address itself).
	/// </summary>
	public int Offset { get; set; }

	public byte Value { get; set; }
}
