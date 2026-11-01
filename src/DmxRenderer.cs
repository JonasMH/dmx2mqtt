using System.Diagnostics;
using Dmx2Mqtt.Effects;
using Dmx2Mqtt.Output;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt;

/// <summary>
/// Renders the current effect and sends it to the DMX interface at a fixed frame rate.
/// Runs on its own thread, since the output blocks while a frame is on the wire.
/// </summary>
public sealed class DmxRenderer(
	ILogger<DmxRenderer> logger,
	IOptions<DmxOptions> options,
	LightState state,
	FixtureMap map,
	DmxOutputFactory outputFactory,
	Dmx2MqttMetrics metrics) : BackgroundService
{
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

	private readonly DmxOptions _options = options.Value;

	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		Task.Factory.StartNew(() => Run(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

	private void Run(CancellationToken stoppingToken)
	{
		logger.LogInformation("{fixtures} fixture(s), {pixels} pixel(s), {fps} fps",
			_options.Fixtures.Count, map.PixelCount, _options.FrameRate);

		var period = PreciseWait.Ticks(TimeSpan.FromSeconds(1.0 / _options.FrameRate));
		var pixels = new Rgb[map.PixelCount];
		var universe = new byte[_options.Channels];
		var engine = new EffectEngine();
		var clock = Stopwatch.StartNew();
		var level = 0.0;
		var last = 0.0;

		while (!stoppingToken.IsCancellationRequested)
		{
			IDmxOutput? output = null;
			try
			{
				output = outputFactory.Create();
				output.Open();
				metrics.SetOutputConnected(true);

				var next = Stopwatch.GetTimestamp();
				while (!stoppingToken.IsCancellationRequested)
				{
					var now = clock.Elapsed.TotalSeconds;
					var dt = now - last;
					last = now;

					var snapshot = state.Snapshot();
					level = StepLevel(level, snapshot, dt);
					engine.Render(pixels, snapshot.Effect, snapshot.Color, now, dt);
					map.Write(pixels, Math.Pow(level, _options.Gamma), universe);

					output.Send(universe);
					metrics.FrameSent();

					next += period;
					if (Stopwatch.GetTimestamp() > next)
					{
						// Behind (slow output or a long frame), don't try to catch up
						next = Stopwatch.GetTimestamp();
					}
					PreciseWait.Until(next);
				}

				Array.Clear(universe);
				output.Send(universe);
				logger.LogInformation("Stopped, sent blackout");
			}
			catch (Exception e)
			{
				metrics.OutputError();
				logger.LogError(e, "DMX output failed, retrying in {delay} s", RetryDelay.TotalSeconds);
				stoppingToken.WaitHandle.WaitOne(RetryDelay);
			}
			finally
			{
				output?.Dispose();
				metrics.SetOutputConnected(false);
			}
		}
	}

	/// <summary>
	/// Moves the master level towards the target brightness over <see cref="DmxOptions.FadeSeconds"/>.
	/// </summary>
	private double StepLevel(double level, LightSnapshot snapshot, double dt)
	{
		var target = snapshot.On ? snapshot.Brightness / 255.0 : 0;
		if (_options.FadeSeconds <= 0)
		{
			return target;
		}
		var step = dt / _options.FadeSeconds;
		return level < target ? Math.Min(target, level + step) : Math.Max(target, level - step);
	}
}
