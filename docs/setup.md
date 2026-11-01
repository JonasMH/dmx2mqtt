# Garage lighting: setup and decisions

Notes from planning the garage effect lighting (October–November 2026), so the
reasoning behind the hardware and this service isn't lost.

## The garage and the house

- A garage of about 45 m², used as a workshop and for other things where more
  comfortable light is wanted. Sometimes used for Halloween, with lights carried outside.
- The house runs on **KNX**. KNX is too slow for effects (the bus manages a few dozen
  telegrams per second), but it should be able to trigger them.
- **Home Assistant** with an MQTT broker. The user maintains several `*2Mqtt` services
  and their own discovery library (`HomeAssistantDiscoveryNet` / `ToMqttNet`).

## Light layers

| Layer | What | Controlled by |
|---|---|---|
| Work and ambient light | The existing LED panels, to get a dimmer | KNX (dimming actuator or DALI gateway) |
| Effects | DMX LED bars | Home Assistant → MQTT → Dmx2Mqtt → USB-DMX |

The panels are the fallback: they work from KNX alone, without Home Assistant or this
service. Dimmed panels are fine as ambient light. Cool-white panels still look cool
when dimmed, and cheap drivers can flicker at low levels.

Because the panels give the white light, the effect fixtures can be RGB without white.

## Options considered

- **LED strips (WLED, ESP32) around the ceiling:** possible, but the effects would be
  tied to one place. Dropped in favour of fixtures that can be moved outside.
- **Battery uplights with wireless DMX:** easy to carry outside, but batteries wear out
  and cheap models use their own radio protocols. The user prefers wired.
- **IP65 fixtures (PARs, bars):** right for outdoor use, but 3–5× the price. Postponed;
  indoor fixtures can still go outside in dry weather, under a roof or aimed out
  through the garage door.
- **Round PAR spots vs. bars:** bars on the floor along the walls light the wall evenly
  (wall washing), and with segment control effects can run along them. Bars chosen.
- **Art-Net/sACN node** (e.g. Eurolite DXT PoE Art-Net Node I, about €155): the clean
  network solution, but it costs about as much as the bars. Postponed.
- **Stairville RJ45 DMX Shuttle Snake:** not a converter. It is a passive cable that
  carries DMX lines over Cat cable, and must never be plugged into a switch.

## Ordered (Thomann, 1.623 kr)

| Item | Qty |
|---|---|
| Stairville LED Bar 240/8 RGB DMX 30° | 2 |
| Stairville PDC3CC DMX cable, 3-pin, 5 m | 1 |
| Stairville PDC3CC DMX cable, 3-pin, 10 m | 1 |
| ADJ DMX terminator set, 3-pin | 1 |
| Eurolite USB-DMX512 Interface/Update Adapter | 1 |

Wiring:

```
Linux box (Dmx2Mqtt) → USB → Eurolite dongle → DMX cable → bar 1 → DMX cable → bar 2 → terminator
```

The Stairville LED Bar 240/8 has 2, 3, 5 and 24-channel modes. The 24-channel mode is
8 segments × RGB (channels 1, 4, 7, …), which gives the segment effects. Bar 1 at
address 1, bar 2 at address 25. Check the modes against the manual when the bars
arrive. Also check the power connector and whether a power cable is included.

## Control software

- **QLC+:** the obvious open-source choice. It supports the dongle and has RGB matrix
  effects, but the user found the UI immature and didn't want to learn it.
- **xLights + Falcon Player:** good for Halloween shows synced to music. Possibly
  later, not for everyday use.
- **OLA:** could turn the dongle into an Art-Net node for e.g. `ha-artnet-led`. Another
  layer to run.
- **Chosen: this service (Dmx2Mqtt).** Effects as code, Home Assistant as the UI, MQTT
  discovery like the other services. KNX buttons trigger Home Assistant, which sends
  MQTT light commands.

None of the lighting tools had an MCP server. Home Assistant has an official one, which
can control the light once it is in HA. An MCP interface for the service itself was
deliberately left out for now.

## Open points

- Where the service runs: the USB cable only reaches about 5 m, so it needs a small
  Linux box (e.g. a Raspberry Pi) in the garage, or a long DMX run from where the
  server is.
- Dimmer for the LED panels on KNX: check the driver type (phase cut, 1–10 V, DALI)
  first.
- Later: more bars, IP65 fixtures for outdoor use, an Art-Net node, a 15–20 m DMX cable
  for Halloween.
