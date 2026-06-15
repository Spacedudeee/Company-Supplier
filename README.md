# Company Supplier

An in-game cheat / trainer menu for **Captain of Industry**, the singleplayer factory &
colony-builder by MaFi Games. Give yourself resources, run a full creative/sandbox mode, stop
pollution, command the world map, tweak vehicles and terrain, control the weather, and more —
all from one movable window.

> Built and tested against Captain of Industry **0.8.5.0** (stable game branch only). Singleplayer only.

> ⚠️ **This branch is the v2.0 BETA.** It installs as a **separate mod**
> (`CompanySupplierBeta`, shown in-game as *Company Supplier (BETA)*) that runs **alongside** the
> stable release — it cannot overwrite your stable mod, saves, or config. The stable version lives
> on the `main` branch and as the *Latest* GitHub release; the beta is the **Pre-release** on the
> [Releases page](https://github.com/Spacedudeee/Company-Supplier/releases). This is test software —
> please use a throwaway save and report issues.

## Features

Press **F8** in-game to open the menu. It is organised into tabs:

| Tab | What it does |
|-----|--------------|
| **Resources** | Add any single product or *all* products to your global storage. World-click tools to fill or empty a single storage on the map (e.g. clear out nuclear waste), plus a one-click "fill all storages". |
| **Sandbox** _(new)_ | Creative-mode master switch + individual toggles — run without power, workers, computing, unity or food; instant build; no fuel; no maintenance. **Game speed** beyond the 3× limit (5× / 10× / 20× + uncapped). Unlock the built-in **infinite source/sink** cheat building. **God-tool**: a world-click wand to instantly refuel a shipyard, cargo depot or vehicle. |
| **Environment** _(new)_ | Disable pollution — air, water, landfill, vehicles, ships and trains (master + individual). |
| **World Map** _(new)_ | Reveal the entire world map, unlimited world mines, run mines without unity, a mine-efficiency boost, and a trade boost. |
| **General** | Construction & maintenance toggles, population & supply satisfaction, research unlocks, add Unity points. |
| **Generation** | Set power, computing and Unity levels directly. |
| **Shipyard & Fleet** | World-ship (fleet) controls. |
| **Vehicles** | Disable fuel consumption, raise the vehicle limit, multiply truck cargo capacity. |
| **Terrain** | Instant mine / dump / convert terrain, refill groundwater & oil reserves, plant or remove trees. |
| **Weather** | Lock the weather to a chosen state. |
| **Profile** _(new)_ | Panic-off (disable all continuous cheats at once), auto-restore your setup on load, and 3 save/load preset slots. Backed by a `config.json` in the mod's own folder — it never touches your stable install. |

Product and material names are shown in your game's language.

## Installation (beta)

1. Download `CompanySupplierBeta-v2.0.0-beta.1.zip` from the **Pre-release** (marked *Pre-release*, not
   the *Latest* release) on the [Releases page](https://github.com/Spacedudeee/Company-Supplier/releases) —
   or build it yourself (see below).
2. Fully close Captain of Industry.
3. Extract the zip into `%APPDATA%\Captain of Industry\Mods\`. You get a **new** folder
   `Mods\CompanySupplierBeta\` (containing `manifest.json` + `CompanySupplierBeta.dll`, both pre-configured
   for the beta) next to any existing `CompanySupplier` (stable) folder — do **not** delete or overwrite the
   stable folder; the beta lives alongside it.
4. Launch the game, enable mods in the options, and add **Company Supplier (BETA)** to your save.
5. Press **F8** in-game to open the menu.

> **Test on a NEW or COPIED save**, never your main stable save — this is test software. You can keep
> **both** mod folders installed at once; just don't add both to the **same save**, or you'll get two
> overlapping menus — pick stable *or* beta per save. To uninstall the beta, close the game and delete
> `Mods\CompanySupplierBeta\`; your stable mod, its config and your stable saves are untouched.

## Building from source

You need the **.NET SDK** and a local Captain of Industry installation (the mod compiles
against the game's own assemblies — no separate targeting pack required).

```powershell
# Point COI_ROOT at your install if it differs from the default in build.ps1:
$env:COI_ROOT = "C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry"

# Beta variant — mod id "CompanySupplierBeta", deploys to ...\Mods\CompanySupplierBeta\:
.\build.ps1 -Config Release -ModVariant beta

# Stable variant (the main branch) omits the flag:  .\build.ps1 -Config Release
```

The build compiles `src/CompanySupplier` and automatically deploys the DLL + the matching manifest
into your `%APPDATA%\Captain of Industry\Mods\` folder (the beta variant deploys to
`…\CompanySupplierBeta\`).

## FAQ

**The menu won't open when I press F8.**
Check these in order: mods are enabled in the game options, the mod has been added to your
current save, you're on the **stable** game version (0.8.5.0, not experimental), and
`manifest.json` + `CompanySupplierBeta.dll` are in
`%APPDATA%\Captain of Industry\Mods\CompanySupplierBeta\`.

**Does the beta affect my stable install or saves?**
No. The beta is a separate mod (`CompanySupplierBeta`) in its own folder with its own `config.json`.
Your stable `CompanySupplier` mod, its config, and your stable saves are untouched. Still, treat the
beta as test software and keep test saves disposable.

**Does it work on the experimental branch?**
No — the mod targets stable releases only.

**Does it work in multiplayer?**
No — Captain of Industry is singleplayer, and so is this mod.

**Where are my log files?** (needed for bug reports)
`%USERPROFILE%\Documents\Captain of Industry\Logs` — press **Win + R**, paste that path,
hit Enter, and grab the newest file.

**Product names show in another language.**
That's intentional — names come from the game's own localisation, so they match your game
language.

**A cheat changed my economy in a weird way.**
That's expected — this tool deliberately bypasses the game's economy. Back up your saves.

## Disclaimer

This is a singleplayer cheat tool — it deliberately bypasses the game's normal economy.
Back up your saves before use. Not affiliated with or endorsed by MaFi Games.

## License

[MIT](LICENSE)
