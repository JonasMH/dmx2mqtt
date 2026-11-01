# Microphone capture library over miniaudio (https://miniaud.io).
#
# `make` builds the shared library that `dotnet run` picks up (any backend, linked at
# run time). The Docker image builds the static one: ALSA only and linked directly,
# because a statically linked binary can't dlopen.

MINIAUDIO_VERSION = 0.11.26
MINIAUDIO_SHA256 = 6b376c7fbb1352ba1e0e432ab66069b335ea9405e45cb6c30b97ef0f3c9c3e0e
CFLAGS ?= -O2

shared: build/libdmxaudio.so

static: build/libdmxaudio.a

build/miniaudio.h:
	mkdir -p build
	curl -fsSL -o $@.tmp https://raw.githubusercontent.com/mackron/miniaudio/$(MINIAUDIO_VERSION)/miniaudio.h
	echo "$(MINIAUDIO_SHA256)  $@.tmp" | sha256sum -c -
	mv $@.tmp $@

build/libdmxaudio.so: dmxaudio.c build/miniaudio.h
	$(CC) $(CFLAGS) -shared -fPIC -fvisibility=hidden -Ibuild -o $@ dmxaudio.c -lpthread -lm -ldl

build/libdmxaudio.a: dmxaudio.c build/miniaudio.h
	$(CC) $(CFLAGS) -c -Ibuild $(ALSA_CFLAGS) -DMA_NO_RUNTIME_LINKING \
		-DMA_ENABLE_ONLY_SPECIFIC_BACKENDS -DMA_ENABLE_ALSA -o build/dmxaudio.o dmxaudio.c
	$(AR) rcs $@ build/dmxaudio.o

clean:
	rm -rf build

.PHONY: shared static clean
