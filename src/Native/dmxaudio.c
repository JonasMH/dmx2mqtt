// Microphone capture for Dmx2Mqtt: a few functions over miniaudio, so the C# side
// needs only a handful of P/Invokes and none of miniaudio's large structs.
//
// Mono float samples at the requested rate (miniaudio converts) are passed to a
// callback on miniaudio's capture thread.

#define MINIAUDIO_IMPLEMENTATION
#define MA_NO_ENCODING
#define MA_NO_DECODING
#define MA_NO_RESOURCE_MANAGER
#define MA_NO_NODE_GRAPH
#define MA_NO_ENGINE
#define MA_NO_GENERATION
#include "miniaudio.h"

#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>

#ifdef _WIN32
#define DMX_EXPORT __declspec(dllexport)
#else
#define DMX_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __linux__
#include <dlfcn.h>

static void ignore_alsa_error(const char* file, int line, const char* function, int err, const char* format, ...)
{
	(void)file;
	(void)line;
	(void)function;
	(void)err;
	(void)format;
}

// alsa-lib prints every failed attempt to stderr, dozens of lines each time the
// service retries. The devices are listed in the error message instead.
static void quiet_alsa(void)
{
#ifdef MA_NO_RUNTIME_LINKING
	snd_lib_error_set_handler(ignore_alsa_error);
#else
	void* alsa = dlopen("libasound.so.2", RTLD_NOW | RTLD_NOLOAD);
	if (alsa)
	{
		int (*set_handler)(void*) = (int (*)(void*))dlsym(alsa, "snd_lib_error_set_handler");
		if (set_handler)
		{
			set_handler((void*)ignore_alsa_error);
		}
		dlclose(alsa);
	}
#endif
}
#else
static void quiet_alsa(void)
{
}
#endif

typedef void (*dmx_audio_callback)(void* user, const float* samples, ma_uint32 count);

typedef struct
{
	ma_context context;
	ma_device device;
	dmx_audio_callback callback;
	void* user;
	char name[256];
} dmx_audio;

static void on_data(ma_device* device, void* output, const void* input, ma_uint32 count)
{
	(void)output;
	dmx_audio* audio = (dmx_audio*)device->pUserData;
	audio->callback(audio->user, (const float*)input, count);
}

static int contains(const char* text, const char* part)
{
	for (; *text; text++)
	{
		const char* a = text;
		const char* b = part;
		while (*a && *b && tolower((unsigned char)*a) == tolower((unsigned char)*b))
		{
			a++;
			b++;
		}
		if (!*b)
		{
			return 1;
		}
	}
	return 0;
}

static void append(char* buffer, int length, const char* text)
{
	size_t used = strlen(buffer);
	if (used + 1 < (size_t)length)
	{
		snprintf(buffer + used, (size_t)length - used, "%s", text);
	}
}

static void append_devices(char* buffer, int length, ma_device_info* captures, ma_uint32 count)
{
	append(buffer, length, ". Capture devices:");
	for (ma_uint32 i = 0; i < count; i++)
	{
		append(buffer, length, i == 0 ? " " : "; ");
		append(buffer, length, captures[i].name);
	}
	if (count == 0)
	{
		append(buffer, length, " none");
	}
}

// Starts capturing from the first device whose name contains `device` (any case), or
// the default device when it is NULL or empty. Returns NULL and a message in `error`
// on failure.
DMX_EXPORT dmx_audio* dmx_audio_open(const char* device, ma_uint32 sample_rate,
	dmx_audio_callback callback, void* user, char* error, int error_length)
{
	error[0] = 0;
	dmx_audio* audio = (dmx_audio*)calloc(1, sizeof *audio);
	if (!audio)
	{
		snprintf(error, (size_t)error_length, "Out of memory");
		return NULL;
	}
	audio->callback = callback;
	audio->user = user;

	ma_result result = ma_context_init(NULL, 0, NULL, &audio->context);
	if (result != MA_SUCCESS)
	{
		snprintf(error, (size_t)error_length, "No audio backend: %s", ma_result_description(result));
		free(audio);
		return NULL;
	}
	quiet_alsa();

	ma_device_info* captures = NULL;
	ma_uint32 count = 0;
	if (ma_context_get_devices(&audio->context, NULL, NULL, &captures, &count) != MA_SUCCESS)
	{
		count = 0;
	}

	ma_device_id* id = NULL;
	if (device && *device)
	{
		for (ma_uint32 i = 0; i < count && !id; i++)
		{
			if (contains(captures[i].name, device))
			{
				id = &captures[i].id;
			}
		}
		if (!id)
		{
			snprintf(error, (size_t)error_length, "No capture device matches \"%s\"", device);
			append_devices(error, error_length, captures, count);
			ma_context_uninit(&audio->context);
			free(audio);
			return NULL;
		}
	}

	ma_device_config config = ma_device_config_init(ma_device_type_capture);
	config.capture.pDeviceID = id;
	config.capture.format = ma_format_f32;
	config.capture.channels = 1;
	config.sampleRate = sample_rate;
	config.dataCallback = on_data;
	config.pUserData = audio;

	result = ma_device_init(&audio->context, &config, &audio->device);
	if (result != MA_SUCCESS)
	{
		snprintf(error, (size_t)error_length, "Failed to open the capture device: %s", ma_result_description(result));
		append_devices(error, error_length, captures, count);
		ma_context_uninit(&audio->context);
		free(audio);
		return NULL;
	}

	ma_device_get_name(&audio->device, ma_device_type_capture, audio->name, sizeof audio->name, NULL);

	result = ma_device_start(&audio->device);
	if (result != MA_SUCCESS)
	{
		snprintf(error, (size_t)error_length, "Failed to start capturing: %s", ma_result_description(result));
		ma_device_uninit(&audio->device);
		ma_context_uninit(&audio->context);
		free(audio);
		return NULL;
	}
	return audio;
}

DMX_EXPORT const char* dmx_audio_name(dmx_audio* audio)
{
	return audio->name;
}

// 0 when the device stopped by itself, e.g. a USB microphone was unplugged
DMX_EXPORT int dmx_audio_is_started(dmx_audio* audio)
{
	return ma_device_get_state(&audio->device) == ma_device_state_started;
}

// Stops capturing; no callback runs after this returns
DMX_EXPORT void dmx_audio_close(dmx_audio* audio)
{
	ma_device_uninit(&audio->device);
	ma_context_uninit(&audio->context);
	free(audio);
}
