# CodeMarket.ModAPI

Non-destructive modding framework, zero-code JSON data pack loader, and first-party reference QoL mod for [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) (Unity 6 x64 Mono). Works identically on **Windows native Steam** and **Linux Steam Proton**.

## Features (v1.0.0)
* **Unconditional Leaderboard Protection:** Replaces `SteamDayBoard` with *Code Market*'s built-in `NoDayBoard` whenever mods are active so public `"BestDay"` Steam leaderboards stay clean.
* **Steam Achievements Friendly:** Achievements remain enabled by default (with opt-in `DisableAchievementsWhenModded = true` in `BepInEx/config/CodeMarket.ModAPI.cfg`).
* **C# Plugin API (`ICodeMarketMod`):** Register product/upgrade/store-rate overrides, custom `Robot` & `Store` scripting methods callable from **both Java and C#**, procedural 3D material color overrides, and per-save sidecar data.
* **Zero-Code JSON Data Packs (`mod.json`):** Drop a `mod.json` folder into `BepInEx/plugins/` to tweak prices, upgrade costs, store rates, and material colors without writing C#.
* **Per-Save Sidecar Persistence:** Mod state saves atomically to `<save>.moddata.json` alongside vanilla `save.json` (removing mods leaves vanilla saves untouched).
* **Fault Isolation & Mod Menu (`F8`):** Failing mods are individually quarantined (*Continue-with-Warning*) with an in-game banner and scrollable `F8` Mod Menu.

---

## Installation

### 1. Install BepInEx 5 (x64 Mono)
Download **`BepInEx_win_x64_5.4.23.6.zip`** (or newer 5.4.x x64) from [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases) and extract it into your [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) folder (next to `Code Market.exe`).

* **Windows Steam:** No launch options needed.
* **Linux Steam Proton:** Right-click **Code Market → Properties → General → Launch Options** and set:
  ```text
  WINEDLLOVERRIDES="winhttp=n,b" %command%
  ```

### 2. Install `CodeMarket.ModAPI`
Extract `CodeMarket.ModAPI-v1.0.0-github.zip` directly into your [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) folder (or install the Thunderstore ZIP via r2modman/Gale).

Press **`F8`** in-game to open the Mod Menu.

---

## Creating Your Own Mods
See **[MODDING_GUIDE.md](MODDING_GUIDE.md)** for complete instructions and copy-paste templates for:
* Zero-code **JSON Data Packs** (`mod.json`)
* C# **BepInEx Plugin Mods** (`ICodeMarketMod`, custom Java & C# scripting methods, material overrides, and per-save sidecar state)

---

## Building from Source
Requires **.NET SDK 10+** (or .NET 8+ with `netstandard2.1` targeting pack) and a local *Code Market* installation with BepInEx 5 extracted:

```bash
export GAME_DIR="/path/to/steamapps/common/Code Market"
dotnet build CodeMarket.ModAPI.sln -c Release -p:TreatWarningsAsErrors=true
dotnet run --project tests/CodeMarket.ModAPI.Core.Tests -c Release --no-build

# Deploy to local game installation
./scripts/deploy-local.sh

# Build GitHub + Thunderstore release ZIPs in dist/
./scripts/package-release.sh
```

## License
MIT License — see [LICENSE](LICENSE).
