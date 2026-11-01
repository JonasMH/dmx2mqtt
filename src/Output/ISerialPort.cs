namespace Dmx2Mqtt.Output;

/// <summary>
/// The serial calls <see cref="OpenDmxOutput"/> needs; .NET's SerialPort cannot set
/// 250 kbaud on Linux or hold a break for a given time.
/// </summary>
public interface ISerialPort : IDisposable
{
	/// <summary>
	/// Opens the device at the baud rate with 8 data bits, no parity and 2 stop bits.
	/// </summary>
	void Open(uint baudRate);

	void SetBreak(bool on);

	void Write(ReadOnlySpan<byte> data);

	/// <summary>
	/// Blocks until the written data has left the driver.
	/// </summary>
	void Drain();
}
