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

## Microphone

`src/Native/dmxaudio.c` wraps miniaudio in four functions so the C# side (`DmxAudio`)
needs no miniaudio structs. miniaudio.h is downloaded at build time (pinned version +
sha256, in both `src/Native/Makefile` and `build.cmd`), not checked in. Alpine has no
static alsa-lib, so the Dockerfile builds one from source and links it with
`DirectPInvoke`/`NativeLibrary` (`-p:DmxAudioLibrary=...`); everywhere else the library
is loaded from next to the binary. `BeatDetector` is plain C# and tested with synthetic
drum loops.

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

- `make start` on WSL needs `libpulse0` installed (the user's machine; not installed in
  this session's environment). ALSA alone sees no cards in WSL.
- Microphone without one: `apt-get download` libpulse0 + pulseaudio-utils (and their
  libs) into the scratchpad, `pactl load-module module-null-sink` on WSLg's PulseServer,
  `paplay` a generated drum loop into it and set `Audio__Device` to the sink's monitor.
  Unload the module afterwards. For the Docker image's static ALSA, mount an
  `.asoundrc` with a `file` plugin (`infile`) over `null`.
- `make build-image-arm64` fails here with "exec format error": QEMU binfmt isn't set up.
- Windows: the WSL machine has a Windows .NET (only SDK 9 so far) and the VS C++ tools.
  A self-contained `win-x64` publish from Linux (`-p:PublishAot=false`) runs there.
  COM4 on that machine is a METAPACE printer port; don't write to it. `set X=` in cmd
  removes a variable instead of emptying it; pass empty settings as arguments instead
  (`Dmx2Mqtt.exe --Dmx:Device= --MqttConnection:Server=`). `make native-windows` builds
  `dmxaudio.dll`; the machine has a C920 webcam microphone. Loopback (the user plays
  Spotify on this PC) is tested by playing a WAV with PowerShell's
  `Media.SoundPlayer`; keep it quiet, the user hears it. `curl` from WSL can't reach
  the Windows process on localhost, so check the log instead.

## Not verified yet

- The real DMX timing on the Eurolite dongle (FTDI FIFO drain vs. the next break, see
  `UsbMargin`) and the Stairville 24-channel mapping. Both arrive with the hardware.
- The Windows COM path against a real FTDI dongle, and a native AOT publish for
  `win-x64` (needs the .NET 10 SDK on Windows).
