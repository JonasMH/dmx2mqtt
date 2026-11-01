# CLAUDE.md

## Why this service exists

Garage effect lighting: DMX bars driven from Home Assistant (and through it from KNX).
The background, hardware and decisions are in [docs/setup.md](docs/setup.md).

The short version:

- The LED panels (on KNX) give the everyday light. The DMX bars are only for effects
  and Halloween, so the light is RGB without white.
- Lighting-console software was rejected. QLC+ felt immature to the user, and learning
  it to build effects wasn't worth it for two bars. Effects are plain C# instead, and
  Home Assistant is the UI.
- The interface is a cheap FTDI "Open DMX" USB dongle, not an Art-Net node, so the
  service has to generate the DMX timing itself (`OpenDmxOutput`).
- MQTT + discovery, like the user's other `*2Mqtt` services, so KNX → HA → MQTT needs no
  new integrations.

## Conventions

Follow the user's other services (`~/projects/Dantherm2Mqtt` is the closest):
`WebApplication.CreateSlimBuilder`, `ToMqttNet` / `HomeAssistantDiscoveryNet` (the
user's own libraries, source in `~/projects/HomeAssistantDiscoveryNet`), Native AOT on
Alpine musl, `/health` + `/metrics`, tabs, xunit v3.

Everything must stay AOT and trim safe: source-generated JSON (`Dmx2MqttJsonContext`),
`LibraryImport` for libc. `DirectPInvoke` for libc is needed because the binary is
statically linked. Check with `make build-image`; plain `dotnet build` doesn't show
every AOT warning, and this machine has no clang for a local AOT publish.

## Git

New repo with no remote yet. Commit to `main`.

## Testing without hardware

- `Dmx:Device` empty runs with `NullDmxOutput`.
- The Open DMX path can be exercised against a pseudo-terminal (Python `pty.openpty()`,
  `Dmx__Device=/dev/pts/N`). That checks the termios2/break/write calls, not the real
  line timing.
- MQTT end to end: a throwaway `eclipse-mosquitto:2` container on a docker network with
  `allow_anonymous true`. Don't test against the user's real broker: discovery would
  create entities in their Home Assistant.

## Not verified yet

- The real DMX timing on the Eurolite dongle (FTDI FIFO drain vs. the next break, see
  `UsbMargin`) and the Stairville 24-channel mapping. Both arrive with the hardware.
