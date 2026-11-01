using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt.Test;

public class MqttLightHandlerTests
{
	private readonly MqttConnectionServiceMock _mqtt = new();
	private readonly LightState _state = new();
	private readonly MqttLightHandler _sut;

	public MqttLightHandlerTests()
	{
		_sut = new MqttLightHandler(
			NullLogger<MqttLightHandler>.Instance,
			_mqtt,
			_state,
			Microsoft.Extensions.Options.Options.Create(new DmxOptions { Name = "Garage" }));
	}

	[Fact]
	public async Task CommandUpdatesAndPublishesState()
	{
		await _sut.StartAsync(TestContext.Current.CancellationToken);

		await _sut.HandleMessageAsync("dmx2mqtt/light/set",
			"""{"state":"ON","brightness":128,"color":{"r":255,"g":0,"b":10},"effect":"Fire"}""");

		var snapshot = _state.Snapshot();
		Assert.True(snapshot.On);
		Assert.Equal(128, snapshot.Brightness);
		Assert.Equal("Fire", snapshot.Effect);

		var message = Assert.Single(_mqtt.PublishedMessages);
		Assert.Equal("dmx2mqtt/light/state", message.Topic);
		Assert.True(message.Retain);
		var json = JsonNode.Parse(Encoding.UTF8.GetString(message.PayloadSegment))!;
		Assert.Equal("ON", (string?)json["state"]);
		Assert.Equal("rgb", (string?)json["color_mode"]);
		Assert.Equal(10, (int?)json["color"]!["b"]);
		Assert.Equal("Fire", (string?)json["effect"]);

		// Commands win over a retained state arriving later
		Assert.DoesNotContain("dmx2mqtt/light/state", _mqtt.Subscriptions);
	}

	[Fact]
	public async Task RestoresRetainedStateOnce()
	{
		await _sut.StartAsync(TestContext.Current.CancellationToken);

		await _sut.HandleMessageAsync("dmx2mqtt/light/state", """{"state":"ON","effect":"Spooky"}""");
		await _sut.HandleMessageAsync("dmx2mqtt/light/state", """{"state":"OFF","effect":"Fire"}""");

		Assert.True(_state.Snapshot().On);
		Assert.Equal("Spooky", _state.Snapshot().Effect);
		Assert.Empty(_mqtt.PublishedMessages);
		Assert.Equal(["dmx2mqtt/light/set"], _mqtt.Subscriptions);
	}

	[Fact]
	public async Task IgnoresUnknownEffectAndInvalidJson()
	{
		await _sut.HandleMessageAsync("dmx2mqtt/light/set", """{"state":"ON","effect":"Disco"}""");
		await _sut.HandleMessageAsync("dmx2mqtt/light/set", "not json");

		Assert.True(_state.Snapshot().On);
		Assert.Equal("Solid", _state.Snapshot().Effect);
		Assert.Single(_mqtt.PublishedMessages);
	}

	[Fact]
	public async Task PublishesJsonLightDiscovery()
	{
		await _sut.PublishDiscoveryAsync();

		var message = Assert.Single(_mqtt.PublishedMessages);
		Assert.Equal("homeassistant/light/dmx2mqtt/dmx2mqtt_light/config", message.Topic);
		var json = JsonNode.Parse(Encoding.UTF8.GetString(message.PayloadSegment))!;
		Assert.Equal("json", (string?)json["schema"]);
		Assert.Equal("dmx2mqtt/light/set", (string?)json["command_topic"]);
		Assert.Equal("rgb", (string?)json["supported_color_modes"]![0]);
		Assert.Equal(9, json["effect_list"]!.AsArray().Count);
		Assert.Equal("2", (string?)json["availability"]![0]!["payload_available"]);
		Assert.Equal("Garage", (string?)json["device"]!["name"]);
	}

	[Fact]
	public async Task BrightnessZeroTurnsOff()
	{
		await _sut.HandleMessageAsync("dmx2mqtt/light/set", """{"state":"ON","brightness":0}""");

		Assert.False(_state.Snapshot().On);
	}
}
