# Company Supplier — project context for Claude

In-game cheat / trainer menu for **Captain of Industry** (singleplayer factory &
colony-builder by MaFi Games). Built and tested against the **stable** release
**0.8.5.0**. The mod renders an F8 window through the game's own UI framework.

## Hard rules

- **Stable branch only.** The mod is updated for stable releases, never for the
  experimental branch. Experimental-only bug reports are out of scope.
- **Singleplayer only.** No multiplayer / anti-cheat work.
- **Localised UI, English code, reporter-language replies.** In-game/UI display
  strings are German (the mod's menu language); code, identifiers, filenames, and
  commit messages are English; issue/PR replies are written in the **same language
  the reporter used** (default to English if unclear).
- This is a deliberate cheat tool — it bypasses the game economy by design. That is
  expected behaviour, not a bug.

## How to handle issues & PRs

- Reply in the **same language the issue/comment was written in** (default to
  English if unclear), short and friendly.
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
- **Do not hand-edit the version dropdown** in `.github/ISSUE_TEMPLATE/bug_report.yml`.
  The lines between the `# >>> auto-versions >>>` / `# <<< auto-versions <<<` markers are
  generated from GitHub Releases by `.github/workflows/update-issue-versions.yml` (latest
  3 stable + 3 beta). To change the offered versions, publish/edit a release — not the
  file. Everything else in that form (including the static "Built from source" / "Not sure"
  options) is fine to edit by hand.

## FAQ — canned answers for common issues

When an issue matches one of these, answer in the reporter's language (keep it short):

- **Menu won't open / nothing happens on F8.** Check in order: (1) mods are enabled in
  the game options; (2) the mod was added to the current save (it can be added to
  existing saves); (3) the game is on the **stable** version (0.8.5.0), not experimental;
  (4) `manifest.json` and `CompanySupplier.dll` sit in
  `%APPDATA%\Captain of Industry\Mods\CompanySupplier\`.
- **How do I install it?** Copy `manifest.json` + `CompanySupplier.dll` into
  `%APPDATA%\Captain of Industry\Mods\CompanySupplier\`, enable mods in the options, add
  the mod to the save, then press **F8**.
- **Which key opens the menu?** F8.
- **Does it work on the experimental branch?** No — stable releases only.
- **Multiplayer?** No — singleplayer only.
- **Which game version is supported?** Stable 0.8.5.0.
- **Where are the log files?** `%USERPROFILE%\Documents\Captain of Industry\Logs`.
- **Product/material names appear in another language.** Expected — names come from the
  game's own localisation and follow the player's game language.
- **A cheat "broke" my economy / numbers look off.** Expected — the tool bypasses the
  economy by design. Only treat it as a bug if the game crashes or a control does nothing
  at all.
