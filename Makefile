# Development: no DMX output, no MQTT, the default microphone
start: native
	@dotnet run --project src

# The microphone capture library for dotnet run
native:
	@$(MAKE) -s -C src/Native shared

# dmxaudio.dll, from WSL with the Visual Studio C++ tools on Windows
native-windows:
	@cd /mnt/c && cmd.exe /c "$$(wslpath -w $(CURDIR)/src/Native/build.cmd)"

build-image:
	docker build -t dmx2mqtt:latest -f src/Dockerfile .

# Raspberry Pi 4/5 (64-bit OS). Builds under QEMU, so it is slow.
build-image-arm64:
	docker buildx build --platform linux/arm64 --build-arg RID=linux-musl-arm64 \
		-t dmx2mqtt:latest-arm64 -f src/Dockerfile --load .

test:
	dotnet test Dmx2Mqtt.sln

.PHONY: start native native-windows build-image build-image-arm64 test
