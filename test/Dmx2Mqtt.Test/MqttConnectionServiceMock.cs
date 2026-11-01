using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using ToMqttNet;

namespace Dmx2Mqtt.Test;

public class MqttConnectionServiceMock : IMqttConnectionService
{
	public MqttConnectionOptions MqttOptions { get; } = new() { NodeId = "dmx2mqtt" };

	public List<MqttApplicationMessage> PublishedMessages { get; } = [];
	public List<string> Subscriptions { get; } = [];

#pragma warning disable CS0067 // The tests call the handler directly
	public event Func<MqttApplicationMessageReceivedEventArgs, Task>? OnApplicationMessageReceivedAsync;
	public event Func<MqttClientConnectedEventArgs, Task>? OnConnectAsync;
	public event Func<MqttClientDisconnectedEventArgs, Task>? OnDisconnectAsync;
#pragma warning restore CS0067

	public Task PublishAsync(MqttApplicationMessage applicationMessages)
	{
		PublishedMessages.Add(applicationMessages);
		return Task.CompletedTask;
	}

	public Task SubscribeAsync(params MqttTopicFilter[] topics)
	{
		Subscriptions.AddRange(topics.Select(x => x.Topic));
		return Task.CompletedTask;
	}

	public Task UnsubscribeAsync(params string[] topics)
	{
		Subscriptions.RemoveAll(topics.Contains);
		return Task.CompletedTask;
	}
}
