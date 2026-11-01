using System.Diagnostics.Metrics;

namespace Dmx2Mqtt;

public class Dmx2MqttMetrics
{
	public const string MeterName = "Dmx2Mqtt";

	private readonly Counter<long> _framesSent;
	private readonly Counter<long> _outputErrors;
	private int _outputConnected;

	public Dmx2MqttMetrics(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create(MeterName);
		_framesSent = meter.CreateCounter<long>("dmx2mqtt_frames_sent_total", description: "DMX frames sent");
		_outputErrors = meter.CreateCounter<long>("dmx2mqtt_output_errors_total", description: "Failures to open or write the DMX interface");
		meter.CreateObservableGauge("dmx2mqtt_output_connected", () => _outputConnected, description: "Is the DMX interface open");
	}

	public void FrameSent() => _framesSent.Add(1);

	public void OutputError() => _outputErrors.Add(1);

	public void SetOutputConnected(bool connected) => _outputConnected = connected ? 1 : 0;
}
