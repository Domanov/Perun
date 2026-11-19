# Perun

ZEUS (game master) mode for Tushonka.

| | |
|---|---|
| GUID | `com.dmnv.perun` |
| Plugin | `DMNV-Perun` |
| Version | 0.11.2 |
| SPT | 4.1.x (client, BepInEx plugin) |
| Fika | host only |
| License | MIT |

## Features
- **RTS camera**: overhead view of map.
- **Spawning**: spawn all vanilla type bots in groups up to 10. Should support custom factions, at least RUAF and UNTAR work.
- **Orders**: give orders to bots or groups of bots - force movement (ignoring enemies) with ctrl.
- **Take control**: control any of the bots (save for btr) in first person, including inventory,
- **Arsenal**: copy, paste and save loadouts, give random role kits to the selection, give or remove single items, and an arsenal crate to edit a controlled bot's gear with the game's own inventory (DOESNT WORK FOR FIKA YET).
- **Teleport**: move selected bots and players instantly with Alt+right-click.
- **Environment**: time, clock speed, custom clouds, fog, rain, lightning, wind and temperature; save the current weather and reapply it in any raid.

**Compatibility**: 
Checked with:
   - SAIN,
   - MoreBotsAPI factions (own tabs),
   - Smoothie (its object hiding is paused in RTS mode, and a controlled bot is never slowed down or hidden),
   - ORBIT,
   - Realism and FOV Fix by Fontaine (though integration of bots stances was a nightmare).

## Requirements
- SPT 4.1.x
- [DrakiaXYZ-BigBrain] (required)
- Optional: SAIN, MoreBotsAPI, Smoothie, Fika

## Installation
1. Download `DMNV-Perun-<version>.zip` from Releases (Fika groups: `DMNV-Perun-<version>-fika.zip`, which also has the Fika bridge).
2. Extract it into your SPT folder (the one with `EscapeFromTarkov.exe`). You should get `BepInEx\plugins\DMNV-Perun\Perun.Client.dll`.
3. Install BigBrain if you don't have it.
4. Start the game. `BepInEx\LogOutput.log` shows `DMNV-Perun 0.11.2 loaded`.

To uninstall, delete `BepInEx\plugins\DMNV-Perun\`. Saved weather lives in `BepInEx\config\com.dmnv.perun.weather.json`, saved loadouts in `BepInEx\config\com.dmnv.perun.loadouts.json`, and settings in `BepInEx\config\com.dmnv.perun.cfg`.

## Controls
| Key | Action |
|---|---|
| Y | enter / leave RTS mode (F12 to change) |
| WASD, Shift, screen edge | pan, faster, pan with the cursor ("Edge scrolling" in F12) |
| Q/E, middle-drag | rotate |
| Wheel, Alt+wheel | zoom, tilt |
| R / F | floor up / down |
| LMB, drag, Shift | select, box select, add to selection |
| RMB, Shift+RMB, Ctrl+RMB | move, run, forced move |
| X | halt |
| Alt+right-click | teleport the selection |
| G | take control of the selected bot (Y gives it back) |
| K | while controlling: open the bot's inventory with the arsenal crate |
| Del / Ctrl+Del | remove / kill selected |
| L, Ctrl+F, Home | lock camera, fly to selection, back to your body |
| Click the magenta dot, then the map | change where your character returns (right-click cancels) |
| B, T, I | units browser, environment, arsenal |
| N | night vision / thermal (cycles, then off) |
| P | pause the world |
| H, F1 | hide the interface, help |

## Fika
Perun works for the **Fika host** (and in single player). Bots spawned by the host sync to everyone through Fika as usual.

**Headless host:** **UNTESTED USE AT YOUR OWN RISK** the headless machine runs the raid, so one player can be the game master instead:
1. On the headless host, set F12 › *6. Fika game master* › **Game master password** (or `BepInEx\config\com.dmnv.perun.cfg`).
2. The player who should run Perun sets the same password in their own F12, then presses the RTS key in raid.
3. The headless host checks the password and makes them game master; spawning, orders, pause, weather and time are carried out on the host. One game master at a time; leaving RTS mode or disconnecting hands it back. Five wrong passwords lock that player out for a minute.

**Take control in Fika (experimental, host only):** off by default; turn on "Allow take control in Fika" in F12. The bot you control becomes "your player" as in single player.

For pause, weather and time to reach the clients, everyone (host and clients) needs `Perun.Fika.dll` next to `Perun.Client.dll` in `BepInEx\plugins\DMNV-Perun\` (the `-fika` release zip). 
Don't install the bridge without Fika: BepInEx would skip it with a missing-dependency warning. 
Clients without it simply don't see those changes.

## Project layout
```
Client/            Perun.Client (the mod)
  Mode/            RTS mode, god mode, AI ignore, body parking, world pause
  View/            RTS camera, cursor picking and selection
  Units/           spawning, profile loading, unit registry, healing, teleporting
  Arsenal/         item catalogue, inventory operations, loadouts, arsenal crate
  Orders/          move orders (BigBrain layer and logic)
  UI/              interface, theme, panels
  Compat/          Fika, SAIN, BigBrain, MoreBotsAPI, Smoothie, culling
  Patches/         Harmony patches
Fika/              Perun.Fika (Fika packet bridge, experimental)
Tests/             Perun.Tests (checks that run without the game)
```

## License
MIT, see [LICENSE.txt](LICENSE.txt).
