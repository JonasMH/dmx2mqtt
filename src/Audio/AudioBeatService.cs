using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dmx2Mqtt.Effects;
using Microsoft.Extensions.Options;

namespace Dmx2Mqtt.Audio;

/// <summary>
/// Listens to the microphone and keeps the <see cref="BeatClock"/> on the beat of the
/// music while <see cref="Following"/>. Reopens the device when it goes away, e.g. a USB
/// microphone that was unplugged.
/// </summary>
public sealed class AudioBeatService(ILogger<AudioBeatService> logger, IOptions<AudioOptions> options, BeatClock clock)
	: BackgroundService
{
	private const int ErrorLength = 1024;
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

	private readonly ILogger _logger = logger;
	private readonly AudioOptions _options = options.Value;
	private readonly TimeSpan _latency = TimeSpan.FromMilliseconds(options.Value.LatencyMs);
	private volatile BeatDetector? _detector;
	private volatile string? _device;
	private volatile bool _following = true;
	private string? _lastError;
	private bool _locked;

	public bool Enabled => _options.Enabled;

	/// <summary>
	/// Whether the beat clock follows the music. Off while tap tempo is used instead.
	/// </summary>
	public bool Following
	{
		get => _following;
		set => _following = value;
	}

	/// <summary>
	/// The microphone being listened to; null when there is none.
	/// </summary>
	public string? Device => _device;

	/// <summary>
	/// Loudness relative to the recent peak, 0-1; null without a microphone.
	/// </summary>
	public double? Level => _device is not null ? _detector?.Level : null;

	/// <summary>
	/// The tempo of the music while it has a steady beat.
	/// </summary>
	public double? Bpm => _device is not null ? _detector?.Bpm : null;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			return;
		}

		var handle = GCHandle.Alloc(this);
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				nint audio;
				try
				{
					audio = Open(handle);
				}
				catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
				{
					_logger.LogWarning("No microphone: the audio library is missing ({error}). Build it with make -C src/Native", e.Message);
					return;
				}

				if (audio == 0)
				{
					await Task.Delay(RetryDelay, stoppingToken);
					continue;
				}

				try
				{
					while (DmxAudio.IsStarted(audio) != 0)
					{
						await Task.Delay(CheckInterval, stoppingToken);
					}
					_logger.LogWarning("Microphone {device} stopped, reopening", _device);
				}
				finally
				{
					_device = null;
					DmxAudio.Close(audio);
				}
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
		finally
		{
			handle.Free();
		}
	}

	private unsafe nint Open(GCHandle handle)
	{
		// Set before the first callback can run
		_detector = new BeatDetector(_options.SampleRate);

		var error = stackalloc byte[ErrorLength];
		var audio = DmxAudio.Open(string.IsNullOrEmpty(_options.Device) ? null : _options.Device,
			(uint)_options.SampleRate, &OnSamples, GCHandle.ToIntPtr(handle), error, ErrorLength);
		if (audio == 0)
		{
			// Logged once, not on every retry
			var message = Marshal.PtrToStringUTF8((nint)error);
			if (message != _lastError)
			{
				_logger.LogWarning("No microphone: {error}. Retrying every {delay} s", message, RetryDelay.TotalSeconds);
				_lastError = message;
			}
			return 0;
		}

		_lastError = null;
		var name = Marshal.PtrToStringUTF8((nint)DmxAudio.Name(audio));
		_device = string.IsNullOrWhiteSpace(name) ? "the default device" : name;
		_logger.LogInformation("Listening to {device} for the beat", _device);
		return audio;
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static unsafe void OnSamples(nint user, float* samples, uint count)
	{
		var service = (AudioBeatService)GCHandle.FromIntPtr(user).Target!;
		try
		{
			service.Process(new ReadOnlySpan<float>(samples, (int)count));
		}
		catch (Exception e)
		{
			// Must not reach the native capture thread
			service._logger.LogError(e, "Beat detection failed");
		}
	}

	private void Process(ReadOnlySpan<float> samples)
	{
		if (_detector is not { } detector)
		{
			return;
		}

		var estimate = detector.Process(samples);
		if (estimate is { } beat && _following)
		{
			clock.Follow(beat.Bpm, beat.SinceBeat + _latency, Stopwatch.GetTimestamp());
		}

		var locked = detector.Bpm is not null;
		if (locked != _locked)
		{
			_locked = locked;
			if (locked)
			{
				_logger.LogInformation("Beat found: {bpm:F1} BPM", detector.Bpm);
			}
			else
			{
				_logger.LogInformation("Beat lost");
			}
		}
	}
}
