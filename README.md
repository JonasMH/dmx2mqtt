# Dmx2Mqtt

Drives DMX fixtures from an FTDI based "Open DMX" USB interface and exposes them to Home
Assistant as one MQTT light with effects (JSON schema, discovery). Native AOT, no
lighting-console software needed.

The pixels of all configured fixtures form one strip, so effects such as *Chase* or
*Lightning* run across fixtures.

Effects: `Solid`, `Breathe`, `Rainbow`, `Chase`, `Fire`, `Candle`, `Lightning`,
`Strobe`, `Spooky`, `Beat Pulse`, `Beat Chase`, `Party`. `Solid`, `Breathe`, `Chase`,
`Lightning`, `Strobe` and the two Beat effects use the colour chosen in Home Assistant;
the others have their own palette.

`Party` is the auto party mode: on the beat (tap tempo or the microphone), it switches
to a new pattern every 16 beats and new colours every 32, with a strobe fill on the
beat before each change. With a microphone it gets brighter with the music, and it
dims to a slow colour drift when the music has been quiet for a couple of seconds.

## Hardware

* An FTDI based USB-DMX interface without its own microcontroller, e.g. the *Eurolite
  USB-DMX512 Interface/Update Adapter*. It shows up as `/dev/ttyUSB*` through the
  kernel's `ftdi_sio` driver on Linux, or as a COM port through FTDI's VCP driver on
  Windows. The service generates the DMX timing itself (break, mark
  after break, 250 kbaud 8N2), so no libftdi or OLA is needed.
* Interfaces with their own microcontroller (Enttec DMX USB Pro, Eurolite USB-DMX512
  PRO) use a different protocol and are not supported.
* Fixtures: anything with per-pixel or per-fixture RGB(W) channels. The default config is
  two *Stairville LED Bar 240/8 RGB* in 24-channel mode (8 × RGB) at addresses 1 and 25.

## MQTT

`<node>` is `MqttConnection:NodeId` (default `dmx2mqtt`).

| Topic | Content |
|---|---|
| `<node>/connected` | `2` connected, `0` last will (ToMqttNet) |
| `<node>/light/set` | Home Assistant JSON light command |
| `<node>/light/state` | JSON state, retained |
| `homeassistant/light/<node>/<node>_light/config` | discovery |

Example command:

```json
{"state": "ON", "brightness": 200, "color": {"r": 255, "g": 0, "b": 0}, "effect": "Lightning"}
```

The state topic is retained and read back at startup, so a restart keeps the light as it
was. On the first start (no retained state) the light starts off.

## Local display

For parties, `http://<host>:8080/` is a touch page for a tablet or a screen next to the
fixtures. It has a live preview of the bars as they hang (one row per fixture, segments
in the fixture's own order, so `Reverse` shows), on/off, brightness, colours, effects and
tap tempo. Home Assistant stays the everyday UI; the page sends the same commands, so HA
always shows what was set on the display and the other way round.

Tap tempo drives the *Beat Pulse*, *Beat Chase* and *Party* effects (120 BPM until
tapped). With a microphone the page shows its level and whether it hears a beat; a tap
switches to tap tempo, the *Sync* button back to the music. The
taps are timed in the browser, so network latency doesn't skew the tempo. A single tap
moves the beat to now.

There is no login, so anyone on the network can change the lights. The API behind the
page:

| Endpoint | |
|---|---|
| `GET /api/layout` | Name, effects, fixtures with their pixel indices |
| `GET /api/events` | Server-sent events: `state` (light, BPM, output connected, microphone), `pixels` (RRGGBB per pixel, ~20/s) and `level` (microphone 0-1) |
| `POST /api/light` | Same JSON as the MQTT command |
| `POST /api/tempo` | `{"bpm": 128, "since_beat_ms": 0}`; stops following the music |
| `POST /api/audio` | `{"following": true}`: the beat follows the music again |

## Beat from the music

With `Audio:Enabled` the service listens to the music and keeps the beat effects on
the beat: tempo (70–180 BPM, faster music counts in half time) and phase,
found again within a few seconds when the song changes. It finds the beat best in music
with a clear kick drum.

The capture library is a small C wrapper around [miniaudio](https://miniaud.io) in
`src/Native` (WASAPI on Windows, ALSA in the Docker image, PulseAudio or ALSA with
`dotnet run`). Without it the service runs as before and logs that there is no microphone.

* Windows: run `src\Native\build.cmd` (Visual Studio C++ build tools) before
  `dotnet publish`; `dmxaudio.dll` ends up next to `Dmx2Mqtt.exe`. From WSL:
  `make native-windows`.
* Docker: built and linked in. Pass the sound devices with `--device /dev/snd`.
* `make start` builds the Linux library first (`make native`). It needs PulseAudio's
  client library (`sudo apt install libpulse0`); on WSL that gives the Windows
  microphone through WSLg.

`Audio:Device` picks a microphone by part of its name; when nothing matches, the log
lists the devices.

**Music played on the same Windows PC** (e.g. Spotify): on Windows the service listens
to what the PC plays (`Audio:Loopback`, the default there) instead of a microphone; set
it to false for a microphone. That hears the music exactly, with no room
noise, at any volume. `Audio:Device` then picks the output device (empty: the default).
Loopback hears the music before the speakers play it, so the lights can come early,
especially with Bluetooth speakers (100–200 ms). Make `Audio:LatencyMs` smaller or
negative until they match. Loopback needs WASAPI; with PulseAudio on Linux, use the
output's "Monitor of …" source as `Audio:Device` instead.

## Configuration

`appsettings.json`, or environment variables such as `Dmx__Device` and
`MqttConnection__Server`.

| Option | Default | |
|---|---|---|
| `Dmx:Device` | `/dev/ttyUSB0` | `COM3` etc. on Windows. Empty runs without output (dry run) |
| `Dmx:Name` | `Garage DMX` | Device name in Home Assistant |
| `Dmx:FrameRate` | 40 | A 512 channel frame takes about 23 ms on the wire |
| `Dmx:Channels` | 512 | Channels per frame |
| `Dmx:FadeSeconds` | 0.4 | Fade on on/off and brightness changes |
| `Dmx:Gamma` | 2.2 | Applied to the brightness |
| `Dmx:Fixtures` | 2 bars | See below |
| `Audio:Enabled` | false | Follow the beat of the music |
| `Audio:Loopback` | true on Windows | Listen to what the PC plays instead of a microphone (Windows only) |
| `Audio:Device` | | Part of the microphone's (or with loopback, the output's) name; empty is the default device |
| `Audio:LatencyMs` | 60 | Moves the lights earlier to make up for capture and analysis delay; negative delays them |
| `MqttConnection:*` | | Empty `Server` runs without MQTT. `NodeId`, `Server`, `Port`, `Username`, `Password`, `UseTls`, … (ToMqttNet) |

A fixture:

```json
{
  "Name": "Bar 1",
  "Address": 1,
  "Pixels": 8,
  "ColorOrder": "RGB",
  "PixelOffset": 0,
  "Reverse": false,
  "FixedChannels": [{ "Offset": 24, "Value": 255 }]
}
```

* `ColorOrder` is the channel order of one pixel using `R`, `G`, `B` and `W`. With `W`
  the white channel takes the common part of the colour.
* `PixelOffset` skips channels before the first pixel, e.g. a master dimmer.
* `FixedChannels` holds channels at a constant value (offset from the start address).

## Running

```sh
dotnet test Dmx2Mqtt.sln
make start                          # Development: no DMX output, no MQTT
make build-image                    # Native AOT image (linux-musl-x64)
make build-image-arm64              # Raspberry Pi, 64-bit OS (slow, QEMU)

docker run --device /dev/ttyUSB0 --device /dev/snd -e Audio__Enabled=true \
  -e MqttConnection__Server=<broker> -e MqttConnection__Username=... -e MqttConnection__Password=... \
  dmx2mqtt:latest
```

Windows (native, not Docker: Docker Desktop cannot pass the USB dongle through), from
a Windows shell with the .NET 10 SDK and the Visual Studio C++ build tools:

```sh
src\Native\build.cmd
dotnet publish src -r win-x64 -c Release -o out
set Dmx__Device=COM3
set Audio__Enabled=true
out\Dmx2Mqtt.exe
```

The local display is on `/`. `/health` and `/metrics` (Prometheus: frames sent, output errors, interface connected)
are on port 8080.

The USB cable limits how far the interface can be from the fixtures, so the service
runs on a small Linux box next to them, e.g. a Raspberry Pi in the garage.
