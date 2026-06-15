# Company Supplier — project context for Claude

In-game cheat / trainer menu for **Captain of Industry** (singleplayer factory &
colony-builder by MaFi Games). Built and tested against the **stable** release
**0.8.5.0**. The mod renders an F8 window through the game's own UI framework.

## Hard rules

- **Stable branch only.** The mod is updated for stable releases, never for the
  experimental branch. Experimental-only bug reports are out of scope.
- **Singleplayer only.** No multiplayer / anti-cheat work.
- **German for users, English for code.** All in-game/UI display strings and issue
  replies are German; code, identifiers, filenames, and commit messages are English.
- This is a deliberate cheat tool — it bypasses the game economy by design. That is
  expected behaviour, not a bug.

## How to handle issues & PRs

- Reply in **German**, short and friendly.
- Bug reports come from the "Bug report" issue form, feature requests from the
  "Feature request" form. The forms auto-apply `bug` / `enhancement`.
- For crashes, the most important attachment is the log file from
  `%USERPROFILE%\Documents\Captain of Industry\Logs`.
- Do not close issues or open PRs automatically unless explicitly asked.

## Tech & layout

- **Language/build:** C# targeting .NET Framework 4.8, compiled **against the game's
  own assemblies** (`Mafi.*`) — no separate targeting pack. UI uses
  `Mafi.Unity.UiToolkit` (`Row`, `Column`, `Toggle`, `Slider`, `Dropdown`,
  `ButtonText`, `ButtonIconText`, …).
- **Build:** `./build.ps1 -Config Release`. Point `COI_ROOT` at the game install if
  it differs from the default. The build deploys the DLL + `manifest.json` into
  `%APPDATA%\Captain of Industry\Mods\CompanySupplier\`.
- **Source root:** `src/CompanySupplier/`
  - `CompanySupplier.cs` — mod entry point; `CheatService.cs` — central singleton
    that the tabs bind to (no provider injection).
  - `Cheats/` — backend cheat logic (Building, FleetVehicle, Generation, Population,
    Research, StorageTool, Terrain, Weather, …). One file per domain.
  - `UI/Tabs/` — one file per menu tab (RessourcenTab, AllgemeinTab, ErzeugungTab,
    WerftFlotteTab, FahrzeugeTab, GelaendeTab, WetterTab). Tabs implement `ICheatTab`
    and are auto-registered via `[GlobalDependency(RegistrationMode.AsEverything)]`.
  - `UI/CheatWidgets.cs` — shared UI helpers (toggle rows, button groups, proto
    display names). Reuse these instead of hand-rolling controls.
  - `Tools/` — world-click controllers (e.g. `StorageWandController`).
- **UI spec:** `docs/ui-spec.md` describes every control (type, label, values,
  behaviour) per tab. Follow it when adding/changing menu items.

## Conventions

- Commit messages: Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`,
  `chore:`). No attribution trailer.
- Keep files small and focused; prefer reusing `CheatWidgets` helpers.
- Product/material names shown to the user come from the game's localisation
  (`ProtoDisplayName`), so they appear in the player's language automatically.
