using Dmx2Mqtt;
using Dmx2Mqtt.Display;
using Dmx2Mqtt.Effects;
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
if (string.IsNullOrEmpty(builder.Configuration[$"{MqttConnectionOptions.Section}:Server"]))
{
	services.AddOptions<MqttConnectionOptions>().BindConfiguration(MqttConnectionOptions.Section);
	services.AddSingleton<NoMqttConnectionService>();
	services.AddSingleton<IMqttConnectionService>(sp => sp.GetRequiredService<NoMqttConnectionService>());
	services.AddHostedService(sp => sp.GetRequiredService<NoMqttConnectionService>());
}
else
{
	services.AddMqttConnection();
}
services.AddOptions<DmxOptions>().BindConfiguration(DmxOptions.Section);

services.ConfigureHttpJsonOptions(options =>
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, Dmx2MqttJsonContext.Default));

services.AddSingleton<LightState>();
services.AddSingleton<BeatClock>();
services.AddSingleton<LivePreview>();
services.AddSingleton<FixtureMap>();
services.AddSingleton<DmxOutputFactory>();
services.AddSingleton<Dmx2MqttMetrics>();
services.AddHostedService<DmxRenderer>();
services.AddSingleton<MqttLightHandler>();
services.AddHostedService(sp => sp.GetRequiredService<MqttLightHandler>());

services.AddOpenTelemetry()
	.WithMetrics(builder =>
	{
		builder.AddPrometheusExporter();
		builder.AddMeter(Dmx2MqttMetrics.MeterName, "ToMqttNet");
	});

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");
app.MapDisplay();

app.Run();
