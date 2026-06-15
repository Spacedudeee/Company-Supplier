# Company Supplier

An in-game cheat / trainer menu for **Captain of Industry**, the singleplayer factory &
colony-builder by MaFi Games. Give yourself resources, tweak vehicles and terrain, control
the weather, and more — all from one movable window.

> Built and tested against Captain of Industry **0.8.5.0**. Singleplayer only.

## Features

Press **F8** in-game to open the menu. It is organised into tabs:

| Tab | What it does |
|-----|--------------|
| **Resources** | Add any single product or *all* products to your global storage. World-click tools to fill or empty a single storage on the map (e.g. clear out nuclear waste), plus a one-click "fill all storages". |
| **General** | Construction & maintenance toggles, population & supply satisfaction, research unlocks, add Unity points. |
| **Generation** | Set power, computing and Unity levels directly. |
| **Shipyard & Fleet** | World-ship (fleet) controls. |
| **Vehicles** | Disable fuel consumption, raise the vehicle limit, multiply truck cargo capacity. |
| **Terrain** | Instant mine / dump / convert terrain, refill groundwater & oil reserves, plant or remove trees. |
| **Weather** | Lock the weather to a chosen state. |

Product and material names are shown in your game's language.

## Installation

1. Grab the latest release, or build it yourself (see below).
2. Copy `manifest.json` and `CompanySupplier.dll` into:
   `%APPDATA%\Captain of Industry\Mods\CompanySupplier\`
3. Launch the game, enable mods in the options, and add the mod to your save
   (the manifest allows adding to / removing from existing saves).
4. Press **F8** in-game to open the menu.

## Building from source

You need the **.NET SDK** and a local Captain of Industry installation (the mod compiles
against the game's own assemblies — no separate targeting pack required).

```powershell
# Point COI_ROOT at your install if it differs from the default in build.ps1:
$env:COI_ROOT = "C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry"

.\build.ps1 -Config Release
```

The build compiles `src/CompanySupplier` and automatically deploys the DLL + manifest into your
`%APPDATA%\Captain of Industry\Mods\` folder.

## FAQ

**The menu won't open when I press F8.**
Check these in order: mods are enabled in the game options, the mod has been added to your
current save, you're on the **stable** game version (0.8.5.0, not experimental), and
`manifest.json` + `CompanySupplier.dll` are in
`%APPDATA%\Captain of Industry\Mods\CompanySupplier\`.

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
