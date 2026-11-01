using System.Diagnostics;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dmx2Mqtt.Audio;
using Dmx2Mqtt.Effects;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt.Display;

/// <summary>
/// The local display for parties: a page with a live preview of the fixtures, effect
/// and colour buttons, and tap tempo. Home Assistant stays the everyday UI; both drive
/// the same state through <see cref="MqttLightHandler"/>.
/// </summary>
public static class DisplayEndpoints
{
	private static readonly TimeSpan EventInterval = TimeSpan.FromMilliseconds(50);

	public static void MapDisplay(this WebApplication app)
	{
		app.MapGet("/", () => Results.Stream(
			typeof(DisplayEndpoints).Assembly.GetManifestResourceStream("index.html")!,
			"text/html; charset=utf-8"));

		app.MapGet("/api/layout", (FixtureMap map, IOptions<DmxOptions> options) => new DisplayLayout
		{
			Name = options.Value.Name,
			Effects = EffectFactory.Names,
			Fixtures = [.. map.Layout.Select(x => new DisplayFixture { Name = x.Name, Pixels = x.Pixels })],
		});

		app.MapPost("/api/light", async (LightPayload command, MqttLightHandler handler) =>
		{
			await handler.ApplyAsync(command);
			return Results.NoContent();
		});

		app.MapPost("/api/tempo", (TempoCommand command, BeatClock clock, AudioBeatService audio) =>
		{
			if (!double.IsFinite(command.Bpm) || !double.IsFinite(command.SinceBeatMs))
			{
				return Results.BadRequest();
			}
			// Tapping takes over from the microphone until it is switched back on
			audio.Following = false;
			clock.Set(command.Bpm, TimeSpan.FromMilliseconds(command.SinceBeatMs), Stopwatch.GetTimestamp());
			return Results.NoContent();
		});

		app.MapPost("/api/audio", (AudioCommand command, AudioBeatService audio) =>
		{
			audio.Following = command.Following;
			return Results.NoContent();
		});

		app.MapGet("/api/events", (LightState state, BeatClock clock, AudioBeatService audio, LivePreview preview,
			Dmx2MqttMetrics metrics, IHostApplicationLifetime lifetime, CancellationToken requestAborted) =>
			TypedResults.ServerSentEvents(Events(state, clock, audio, preview, metrics, lifetime.ApplicationStopping, requestAborted)));
	}

	/// <summary>
	/// "state" when the light, tempo, microphone or output changes, "pixels" when the
	/// preview changes, "level" when the microphone level changes.
	/// </summary>
	private static async IAsyncEnumerable<SseItem<string>> Events(LightState state, BeatClock clock,
		AudioBeatService audio, LivePreview preview, Dmx2MqttMetrics metrics, CancellationToken stopping,
		[EnumeratorCancellation] CancellationToken requestAborted)
	{
		// Open streams would otherwise hold up shutdown, and with it the blackout
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopping, requestAborted);
		using var timer = new PeriodicTimer(EventInterval);
		string? lastState = null;
		string? lastPixels = null;
		string? lastLevel = null;

		do
		{
			var current = JsonSerializer.Serialize(new DisplayState
			{
				Light = LightPayload.FromSnapshot(state.Snapshot()),
				Bpm = Math.Round(clock.Bpm, 1),
				OutputConnected = metrics.OutputConnected,
				Audio = audio.Enabled
					? new DisplayAudio { Device = audio.Device, Following = audio.Following, Locked = audio.Bpm is not null }
					: null,
			}, Dmx2MqttJsonContext.Default.DisplayState);
			if (current != lastState)
			{
				lastState = current;
				yield return new SseItem<string>(current, "state");
			}

			var pixels = preview.ToHex();
			if (pixels != lastPixels)
			{
				lastPixels = pixels;
				yield return new SseItem<string>(pixels, "pixels");
			}

			var level = (audio.Level ?? 0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
			if (level != lastLevel)
			{
				lastLevel = level;
				yield return new SseItem<string>(level, "level");
			}
		}
		while (await Tick(timer, cts.Token));
	}

	private static async Task<bool> Tick(PeriodicTimer timer, CancellationToken cancellationToken)
	{
		try
		{
			return await timer.WaitForNextTickAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			return false;
		}
	}
}
