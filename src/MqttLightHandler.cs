using System.Text.Json;
using Dmx2Mqtt.Effects;
using HomeAssistantDiscoveryNet;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using ToMqttNet;

namespace Dmx2Mqtt;

/// <summary>
/// Exposes the fixtures as one Home Assistant JSON light with effects.
///
/// The state topic is retained. At startup the last state is read back from it, so a
/// restart doesn't turn the light off behind Home Assistant's back.
/// </summary>
public sealed class MqttLightHandler : IHostedService
{
	private static readonly TimeSpan RestoreTimeout = TimeSpan.FromSeconds(2);

	private readonly ILogger<MqttLightHandler> _logger;
	private readonly IMqttConnectionService _mqtt;
	private readonly LightState _state;
	private readonly DmxOptions _options;
	private int _restored;

	public string CommandTopic => $"{_mqtt.MqttOptions.NodeId}/light/set";
	public string StateTopic => $"{_mqtt.MqttOptions.NodeId}/light/state";

	public MqttLightHandler(
		ILogger<MqttLightHandler> logger,
		IMqttConnectionService mqtt,
		LightState state,
		IOptions<DmxOptions> options)
	{
		_logger = logger;
		_mqtt = mqtt;
		_state = state;
		_options = options.Value;

		_mqtt.OnConnectAsync += OnConnectAsync;
		_mqtt.OnApplicationMessageReceivedAsync += OnMessageAsync;
	}

	public Task StartAsync(CancellationToken cancellationToken) =>
		_mqtt.SubscribeAsync(
			new MqttTopicFilter { Topic = CommandTopic },
			new MqttTopicFilter { Topic = StateTopic });

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private async Task OnConnectAsync(MqttClientConnectedEventArgs args)
	{
		await PublishDiscoveryAsync();
		_ = PublishDefaultStateAsync();
	}

	/// <summary>
	/// No retained state within the timeout: first start, publish the default state.
	/// </summary>
	private async Task PublishDefaultStateAsync()
	{
		try
		{
			await Task.Delay(RestoreTimeout);
			if (await MarkRestoredAsync())
			{
				_logger.LogInformation("No previous state found");
				await PublishStateAsync(_state.Snapshot());
			}
		}
		catch (Exception e)
		{
			_logger.LogWarning(e, "Failed to publish the default state");
		}
	}

	private Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args) =>
		HandleMessageAsync(args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString());

	internal async Task HandleMessageAsync(string topic, string payload)
	{
		if (topic == CommandTopic)
		{
			if (Parse(payload) is not { } command)
			{
				return;
			}
			await MarkRestoredAsync();
			_logger.LogInformation("Command {payload}", payload);
			await PublishStateAsync(_state.Apply(command));
		}
		else if (topic == StateTopic)
		{
			// Our own state echoing back, or the retained state at startup
			if (Parse(payload) is { } previous && await MarkRestoredAsync())
			{
				var snapshot = _state.Apply(previous);
				_logger.LogInformation("Restored state: {state}, {effect}", snapshot.On ? "on" : "off", snapshot.Effect);
			}
		}
	}

	/// <summary>
	/// Returns true the first time, and stops listening to the state topic.
	/// </summary>
	private async Task<bool> MarkRestoredAsync()
	{
		if (Interlocked.Exchange(ref _restored, 1) == 1)
		{
			return false;
		}
		await _mqtt.UnsubscribeAsync(StateTopic);
		return true;
	}

	private LightPayload? Parse(string payload)
	{
		try
		{
			var command = JsonSerializer.Deserialize(payload, Dmx2MqttJsonContext.Default.LightPayload);
			if (command?.Effect is { } effect && !EffectFactory.Names.Contains(effect))
			{
				_logger.LogWarning("Unknown effect {effect}", effect);
			}
			return command;
		}
		catch (JsonException e)
		{
			_logger.LogWarning("Invalid payload {payload}: {error}", payload, e.Message);
			return null;
		}
	}

	private Task PublishStateAsync(LightSnapshot snapshot) =>
		_mqtt.PublishAsync(
			new MqttApplicationMessageBuilder()
				.WithTopic(StateTopic)
				.WithPayload(JsonSerializer.Serialize(LightPayload.FromSnapshot(snapshot), Dmx2MqttJsonContext.Default.LightPayload))
				.WithRetainFlag()
				.Build());

	internal Task PublishDiscoveryAsync()
	{
		var nodeId = _mqtt.MqttOptions.NodeId;
		var config = new MqttJsonLightDiscoveryConfig
		{
			UniqueId = $"{nodeId}_light",
			CommandTopic = CommandTopic,
			StateTopic = StateTopic,
			Brightness = true,
			SupportedColorModes = ["rgb"],
			Effect = true,
			EffectList = [.. EffectFactory.Names],
			Icon = "mdi:led-strip-variant",
			Availability =
			[
				// Published by ToMqttNet: "2" when connected, "0" as last will
				new MqttDiscoveryAvailablilty { Topic = $"{nodeId}/connected", PayloadAvailable = "2", PayloadNotAvailable = "0" },
			],
			Device = new MqttDiscoveryDevice
			{
				Name = _options.Name,
				Identifiers = [nodeId],
				Manufacturer = "Dmx2Mqtt",
				Model = "Open DMX USB",
			},
		};
		return _mqtt.PublishDiscoveryDocument(config);
	}
}
