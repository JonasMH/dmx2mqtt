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
`LibraryImport` for libc and kernel32. `DirectPInvoke` for libc is needed because the
Linux binary is statically linked (both only for `linux*` RIDs).

It also runs natively on Windows (`win-x64`, FTDI VCP COM port): `ISerialPort` has a
Linux (termios2) and a Windows (Win32 comm API) implementation, and `PreciseWait` raises
the Windows timer resolution to 1 ms. Check with `make build-image`; plain `dotnet build` doesn't show
every AOT warning, and this machine has no clang for a local AOT publish.

## Local display

`/` is a touch page for parties (`src/Display`), next to Home Assistant, which stays the
everyday UI. It's a single `index.html` embedded in the binary (no wwwroot) with minimal
API + SSE, because Blazor Server can't do Native AOT. Display commands go through
`MqttLightHandler.ApplyAsync`, so HA sees them. The `BeatClock` (tap tempo now) is where
audio beat detection would plug in later.

## Git

New repo with no remote yet. Commit to `main`.

## Testing without hardware

- `Dmx:Device` empty runs with `NullDmxOutput`.
- The Open DMX path can be exercised against a pseudo-terminal (Python `pty.openpty()`,
  `Dmx__Device=/dev/pts/N`). That checks the termios2/break/write calls, not the real
  line timing.
- The display: `make start`, open `http://localhost:8581/`. Screenshots work with
  `npx --no-install playwright screenshot --wait-for-timeout=2500 <url> <file>`. Chrome's
  `--virtual-time-budget` never returns because of the event stream.
- MQTT end to end: a throwaway `eclipse-mosquitto:2` container on a docker network with
  `allow_anonymous true`. Don't test against the user's real broker: discovery would
  create entities in their Home Assistant.

- Windows: the WSL machine has a Windows .NET (only SDK 9 so far) and the VS C++ tools.
  A self-contained `win-x64` publish from Linux (`-p:PublishAot=false`) runs there.
  COM4 on that machine is a METAPACE printer port; don't write to it.

## Not verified yet

- The real DMX timing on the Eurolite dongle (FTDI FIFO drain vs. the next break, see
  `UsbMargin`) and the Stairville 24-channel mapping. Both arrive with the hardware.
- The Windows COM path against a real FTDI dongle, and a native AOT publish for
  `win-x64` (needs the .NET 10 SDK on Windows).
