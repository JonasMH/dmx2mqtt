using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using ToMqttNet;

namespace Dmx2Mqtt;

/// <summary>
/// Used when no MQTT server is configured, to run the DMX output and the local display
/// without Home Assistant (the development default).
/// </summary>
public sealed class NoMqttConnectionService(IOptions<MqttConnectionOptions> options, ILogger<NoMqttConnectionService> logger)
	: IMqttConnectionService, IHostedService
{
	public event Func<MqttApplicationMessageReceivedEventArgs, Task>? OnApplicationMessageReceivedAsync { add { } remove { } }
	public event Func<MqttClientConnectedEventArgs, Task>? OnConnectAsync { add { } remove { } }
	public event Func<MqttClientDisconnectedEventArgs, Task>? OnDisconnectAsync { add { } remove { } }

	public MqttConnectionOptions MqttOptions => options.Value;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		logger.LogWarning("No MQTT server configured, running without Home Assistant");
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task PublishAsync(MqttApplicationMessage applicationMessages) => Task.CompletedTask;

	public Task SubscribeAsync(params MqttTopicFilter[] topics) => Task.CompletedTask;

	public Task UnsubscribeAsync(params string[] topics) => Task.CompletedTask;
}
