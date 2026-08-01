# Scav Population

**SPT 4.0.13 Compatible**

Keeps raids populated after the early wave dump: continuous scav reinforcements, PMC squads (1–4), and optional AFK-bot relocation closer to the player.

Developed and tested against **SPT 4.0.13**.

[Latest release](https://github.com/gadjed/Scav-population-SPT-mod/releases/latest) · [License: MIT](LICENSE)

## Features

- **Server:** adds scav reinforcement waves across the full raid (not only the first 10–15 minutes)
- **Server:** spawns **PMC squads of 1–4** on a repeating pulse
- **Client (optional):** detects AFK / standby bots far from you and teleports them onto NavMesh near the player
- Additive to vanilla waves (does not wipe the map’s original spawn table)
- Configurable via `config.json` (server) and F12 (client)

## Install

1. Download `ScavPopulation-*.zip` from [Releases](https://github.com/gadjed/Scav-population-SPT-mod/releases)
2. Extract into your **SPT game root** (folder with `SPT.Server.exe` / `user/`)
3. Restart the SPT server (and game client if you use the AFK plugin)

Paths inside the zip:

```text
user/mods/ScavPopulation/ScavPopulation.dll
user/mods/ScavPopulation/config.json
BepInEx/plugins/ScavPopulation.Client.dll
```

Server log should show lines like:

```text
[ScavPopulation] bigmap: +N scav wave(s), +M PMC squad(s) across X pulse(s)
[ScavPopulation] Ready on N map(s): ...
```

## Config (server)

Edit `user/mods/ScavPopulation/config.json`:

```json
{
  "Enabled": true,
  "Debug": false,
  "StartAfterSeconds": 180,
  "ReinforcementIntervalSeconds": 180,
  "ScavEnabled": true,
  "ScavSlotsMin": 2,
  "ScavSlotsMax": 4,
  "ScavWavesPerInterval": 2,
  "ScavDifficulty": "normal",
  "PmcEnabled": true,
  "PmcSquadMinSize": 1,
  "PmcSquadMaxSize": 4,
  "PmcSquadsPerInterval": 1,
  "PmcDifficulty": "normal",
  "ExtendBotStop": true,
  "SkipMaps": ["laboratory", "labyrinth", "hideout"]
}
```

| Key | Description |
|-----|-------------|
| `StartAfterSeconds` | Delay before reinforcement pulses begin (default `180`) |
| `ReinforcementIntervalSeconds` | Time between pulses (default `180`) |
| `ScavSlotsMin` / `ScavSlotsMax` | Scavs per reinforcement wave |
| `ScavWavesPerInterval` | How many scav waves each pulse adds |
| `PmcSquadMinSize` / `PmcSquadMaxSize` | PMC squad size (clamped 1–4) |
| `PmcSquadsPerInterval` | PMC squads added each pulse |
| `ExtendBotStop` | Extends the map bot-stop window toward raid end |
| `SkipMaps` | Location ids to leave untouched |

## Config (client / F12)

| Setting | Default | Description |
|---------|---------|-------------|
| Enabled | true | Master toggle for AFK relocator |
| StartAfterSeconds | 180 | Wait after raid start |
| CheckIntervalSeconds | 45 | Scan interval |
| AfkSeconds | 60 | Still-time to count as AFK |
| MinDistanceFromPlayer | 100 | Only relocate bots farther than this |
| TeleportMin/MaxDistance | 80–140 | Destination ring around the player |
| MaxTeleportsPerCheck | 2 | Cap per tick |
| IncludeScavs / IncludePmcs | true | Role filters |
| ExcludeBosses | true | Skip bosses / followers |

AFK bots are skipped while in combat (`HaveEnemy` / under fire / not at peace).

## Compatibility

- Additive with vanilla SPT waves
- Heavy spawn overhauls (ABPS, Pulse, Questing Bots spawn system, etc.) may **stack** population — use one primary spawn mod, or lower this mod’s intervals / slots
- Client plugin is optional; server alone already keeps mid/late-raid scav + PMC pulses

## Build from source

Requires **.NET 9** SDK. Client build needs SPT hollowed references under `Client/References/` (`hollowed.dll`, `Comfort.dll`, `spt-reflection.dll`).

```bash
dotnet build Server/ScavPopulation.csproj -c Release
dotnet build Client/ScavPopulation.Client.csproj -c Release
```

Output lands in `Build/SPT/`.

## License

MIT — see [LICENSE](LICENSE).
