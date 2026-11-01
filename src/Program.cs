using Dmx2Mqtt;
using Dmx2Mqtt.Output;
using OpenTelemetry.Metrics;
using ToMqttNet;

var builder = WebApplication.CreateSlimBuilder(args);
var services = builder.Services;

builder.Logging.AddSimpleConsole(options =>
{
	options.IncludeScopes = true;
	options.SingleLine = true;
	options.TimestampFormat = "HH:mm:ss ";
});

services.AddHealthChecks();
services.AddMqttConnection();
services.AddOptions<DmxOptions>().BindConfiguration(DmxOptions.Section);

services.AddSingleton<LightState>();
services.AddSingleton<FixtureMap>();
services.AddSingleton<DmxOutputFactory>();
services.AddSingleton<Dmx2MqttMetrics>();
services.AddHostedService<DmxRenderer>();
services.AddHostedService<MqttLightHandler>();

services.AddOpenTelemetry()
	.WithMetrics(builder =>
	{
		builder.AddPrometheusExporter();
		builder.AddMeter(Dmx2MqttMetrics.MeterName, "ToMqttNet");
	});

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();
