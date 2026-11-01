# Development: no DMX output, broker on localhost
start:
	@dotnet run --project src

build-image:
	docker build -t dmx2mqtt:latest -f src/Dockerfile .

# Raspberry Pi 4/5 (64-bit OS). Builds under QEMU, so it is slow.
build-image-arm64:
	docker buildx build --platform linux/arm64 --build-arg RID=linux-musl-arm64 \
		-t dmx2mqtt:latest-arm64 -f src/Dockerfile --load .

test:
	dotnet test Dmx2Mqtt.sln
