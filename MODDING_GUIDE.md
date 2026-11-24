# Modding Guide — Creating Mods with `CodeMarket.ModAPI`

This guide explains how to build mods for [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) using **`CodeMarket.ModAPI`** (API `v1.0`).

You can create two kinds of mods:
1. **Zero-Code JSON Data Packs (`mod.json`)** — No compiler required. Override product prices, upgrade costs, store rates, and procedural 3D material colors by dropping a single JSON file into `BepInEx/plugins/`.
2. **C# Plugin Mods (`.dll`)** — Full BepInEx plugins implementing `ICodeMarketMod` to add custom **Java & C#** robot/store scripting methods, configurable `.cfg` settings, dynamic economy/visual hooks, and per-save sidecar state.

> **Policy Notice (Coding Languages & DLC):** Per the [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) creators' guidelines, mods may **not** add new in-game programming languages or patch/bypass official Steam language DLC systems (`SteamDlc`, `CodeLanguage`, or official base/DLC language VM assemblies for Java, C#, Python, JavaScript/TypeScript, C++, PHP, Rust, and Go). `CodeMarket.ModAPI` enforces this restriction at runtime by stripping offending patches and quarantining non-compliant mods or data packs.

---

## 1. Creating a Zero-Code JSON Data Pack

### Folder Layout
Create a new folder under your *Code Market* `BepInEx/plugins/` directory containing a `mod.json` file:

```text
Code Market/
└── BepInEx/
    └── plugins/
        └── MyBalancePack/
            └── mod.json
```

### Example `mod.json`
```json
{
  "id": "yourname.my-balance-pack",
  "name": "My Balance Pack",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "author": "Your Name",
  "description": "Adjusts Tomato pricing, rack upgrade costs, and material colors.",
  "type": "datapack",
  "overrides": {
    "products": {
      "Tomato": {
        "sellPrice": 15
      },
      "Bread": {
        "sellPrice": 20
      }
    },
    "upgrades": {
      "racks.tomato": {
        "costMultiplier": 0.75
      },
      "racks.bread": {
        "flatCost": 50
      }
    },
    "storeRates": {
      "scriptCharLimit": 65536,
      "priceMultiplier": 1.1
    },
    "materials": {
      "#FF0000": "#00CC88",
      "#336699": {
        "color": "#5533AA",
        "smoothness": 0.4
      }
    }
  }
}
```

### Manifest & Override Rules
* **`id`** *(required)*: Lowercase unique identifier matching `^[a-z0-9][a-z0-9._-]{1,63}$` (no `..`, spaces, or slashes).
* **`name`** *(required)*: Display name shown in the `F8` Mod Menu.
* **`version`** *(required)*: Semantic version `MAJOR.MINOR.PATCH` (e.g., `1.0.0` or `1.0.0-beta.1`).
* **`apiVersion`** *(required)*: Target ModAPI version (`"1.0"`).
* **`type`** *(required for data packs)*: Must be `"datapack"`.
* **Validation Bounds:**
  * `products.<ProductName>.sellPrice`: integer `>= 0` (uses *Code Market*'s `Product` enum names such as `Tomato`, `Pumpkin`, `Bread`, `Soda`, `Beer`, `Wine`, `Milk`, `Honey`, etc.).
  * `upgrades.<UpgradeId>`: `flatCost` (`>= 0`) or `costMultiplier` (`> 0.0`). If both are set, `flatCost` takes precedence.
  * `storeRates`: `scriptCharLimit` (`>= 1024`), `priceMultiplier` (`> 0.0`), `patienceMultiplier` (`> 0.0`).
  * `materials`: `#RRGGBB` or `#RRGGBBAA` hex color keys mapped to a replacement hex string or `{ "color": "#RRGGBB", "smoothness": 0.0..1.0 }`.
* **Load Priority:** Data packs load in deterministic lexicographic order by `id` (e.g., `z.pack` overrides `a.pack`). Unknown override sections log a non-fatal warning for forward compatibility.

---

## 2. Creating a C# Plugin Mod (`ICodeMarketMod`)

### Prerequisites
* **.NET SDK 8 or 10** (`dotnet` CLI)
* [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) installed locally with **BepInEx 5 (x64 Mono)** and **`CodeMarket.ModAPI`** installed under `BepInEx/plugins/CodeMarket.ModAPI/`

### Step 1: Project File (`MyMod.csproj`)
Target `netstandard2.1` and resolve the game directory via `GAME_DIR` (never hardcode user paths):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>MyAuthor.MyMod</AssemblyName>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <GameDir Condition="'$(GameDir)' == ''">$(GAME_DIR)</GameDir>
    <ManagedDir>$(GameDir)/Code Market_Data/Managed</ManagedDir>
    <BepInExCoreDir>$(GameDir)/BepInEx/core</BepInExCoreDir>
    <ModApiDir>$(GameDir)/BepInEx/plugins/CodeMarket.ModAPI</ModApiDir>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="CodeMarket.ModAPI.Core">
      <HintPath>$(ModApiDir)/CodeMarket.ModAPI.Core.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="CodeMarket.ModAPI.Unity">
      <HintPath>$(ModApiDir)/CodeMarket.ModAPI.Unity.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="BepInEx">
      <HintPath>$(BepInExCoreDir)/BepInEx.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDir)/UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedDir)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### Step 2: Plugin Entry Point (`MyModPlugin.cs`)
Declare a hard BepInEx dependency on `ModApiInfo.PluginGuid`, implement `ICodeMarketMod`, and register inside `Awake()`:

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using CodeMarket.ModAPI.Core;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Json;
using CodeMarket.ModAPI.Unity;
using CodeMarket.ModAPI.Unity.Patches;

namespace MyAuthor.MyMod;

[BepInPlugin("myauthor.mymod", "My Code Market Mod", "1.0.0")]
[BepInDependency(ModApiInfo.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class MyModPlugin : BaseUnityPlugin, ICodeMarketMod
{
    private ConfigEntry<int>? _tomatoPrice;
    private int _runsCounter;

    public string ModId => "myauthor.mymod";
    public string Name => "My Code Market Mod";
    public string Version => "1.0.0";
    public string Description => "Adds custom Tomato pricing, store.greet() for Java/C#, and a blue material reskin.";

    private void Awake()
    {
        // User-editable settings stored in BepInEx/config/myauthor.mymod.cfg
        _tomatoPrice = Config.Bind("Balance", "TomatoPrice", 18, "Override Tomato sell price.");

        // Register with CodeMarket.ModAPI (fault-isolated with automatic rollback on error)
        ModApiRuntime.RegisterPluginMod(this);

        // Optional: persist custom per-save data in <save>.moddata.json
        SaveSidecarPatches.BeforeSidecarSave += OnSave;
        SaveSidecarPatches.AfterSidecarLoad += OnLoad;
    }

    public void Register(ModRegistrationContext context)
    {
        // 1. Balance & Economy override
        context.Balance.SetProductPrice(ModId, "Tomato", _tomatoPrice!.Value);

        // 2. Custom scripting method callable in BOTH Java and C# player scripts
        //    Java:   String greeting = store.greet("Tom");
        //    C#:     string greeting = store.Greet("Tom");
        context.Scripting.RegisterMethod(new ScriptMethodDescriptor(
            ModId,
            ScriptTarget.Store,
            "greet",
            "String",
            "Returns a custom greeting message.",
            new[] { ("String", "who") },
            (storeObj, args) =>
            {
                string who = args.Length > 0 && args[0] is string s ? s : "Shopkeeper";
                _runsCounter++;
                return $"Hello, {who}! (called {_runsCounter}x)";
            }));

        // 3. Procedural 3D material color override
        context.Visuals.RegisterMaterialColorOverride(
            new MaterialColorOverride(ModId, "#FF0000", "#00AAFF", smoothness: 0.35f));

        context.Logger.Info($"[{ModId}] Registered custom hooks.");
    }

    private void OnSave(ISaveSidecarStore sidecars)
    {
        var data = JsonValue.FromObject(new[]
        {
            new KeyValuePair<string, JsonValue>("runsCounter", JsonValue.FromNumber(_runsCounter))
        });
        sidecars.SetModData(ModId, Version, data);
    }

    private void OnLoad(ISaveSidecarStore sidecars)
    {
        if (sidecars.TryGetModData(ModId, out JsonValue? data) && data is not null &&
            data.TryGetProperty("runsCounter", out JsonValue? val) && val is not null)
        {
            _runsCounter = val.AsInt32();
        }
    }
}
```

### Supported Scripting Method Types
When registering methods on `ScriptTarget.Robot` or `ScriptTarget.Store`, `CodeMarket.ModAPI` automatically marshals arguments and return values between C# and *Code Market*'s Java & C# runtimes:

| Java / In-Game Type | C# Handler Parameter / Return Type |
| :--- | :--- |
| `int` / `long` | `int` / `long` (unboxed as `long`) |
| `float` / `double` | `float` / `double` (unboxed as `double`) |
| `boolean` | `bool` |
| `String` | `string` |
| `void` / `null` | `null` |

---

## 3. Building, Installing & Debugging Your Mod

### Build
```bash
export GAME_DIR="/path/to/steamapps/common/Code Market"
dotnet build MyMod.csproj -c Release
```
*(If your machine has no internet access or IPv6 NuGet timeouts, append `--source "$HOME/.nuget/packages"`.)*

### Install
Copy your compiled `MyAuthor.MyMod.dll` into:
```text
Code Market/BepInEx/plugins/MyMod/MyAuthor.MyMod.dll
```

### Verify In-Game
1. Launch [**Code Market**](https://store.steampowered.com/app/5451260/Code_Market/) (on Linux Steam Proton, ensure Launch Options include `WINEDLLOVERRIDES="winhttp=n,b" %command%`).
2. Press **`F8`** to open the **CodeMarket.ModAPI Mod Menu**:
   * **Green `Active`:** Your mod validated and registered cleanly.
   * **Red `Quarantined`:** Your mod threw an error or failed schema validation. The game continues running safely with your mod's partial registrations rolled back. Click **Details (`F8`)** or inspect `Code Market/BepInEx/LogOutput.log` for the exact phase and exception message.
3. **Leaderboard Safety:** All modded sessions automatically run with `NoDayBoard` so public Steam leaderboards are never affected. Vanilla `save.json` files are never modified; mod state lives in `<save>.moddata.json`.
