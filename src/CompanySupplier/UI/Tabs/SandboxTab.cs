using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Sandbox": echter Kreativmodus. Ein Master-Schalter aktiviert auf einmal alle
    /// "laeuft-trotz-Mangel"-Cheats (kein Strom/Arbeiter/Computing/Unity/Lebensmittel noetig) plus
    /// Sofortbau, Treibstoff-aus und Wartung-aus. Darunter Einzelschalter zur Feinsteuerung sowie die
    /// Spielgeschwindigkeit ueber das normale 3x-Limit hinaus.
    ///
    /// Bindet ueber <c>CheatService.Instance</c>. Registrierung per <c>[GlobalDependency(AsEverything)]</c>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class SandboxTab : ICheatTab
    {
        private readonly UiComponent _content;

        // Referenzen auf die Einzel-Toggles, damit der Master-Schalter sie auch optisch mitzieht.
        private Toggle _noPower, _noWorkers, _noComputing, _noUnity, _noFood, _instaBuild, _noFuel, _noMaintenance;

        // Unterdrueckt die onChanged-Backend-Aufrufe der Kinder, waehrend der Master sie optisch setzt.
        private bool _suppress;

        public SandboxTab()
        {
            _content = BuildContent();
        }

        public string Name => "Sandbox";

        // Leerer Pfad = kein Icon (robust). Kann in einer Politur-Runde durch ein verifiziertes Asset ersetzt werden.
        public string IconPath => "";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            // Erst die Einzel-Toggles in ihre Felder bauen (Collection-Initializer erlauben keine Zuweisung).
            _noPower = BuildIgnoreToggle("Kein Strom nötig", () => Svc?.Sandbox?.NoPowerNeeded ?? false,
                v => Svc?.Sandbox?.SetNoPowerNeeded(v),
                "Maschinen laufen weiter, auch wenn nicht genug Strom da ist.");
            _noWorkers = BuildIgnoreToggle("Keine Arbeiter nötig", () => Svc?.Sandbox?.NoWorkersNeeded ?? false,
                v => Svc?.Sandbox?.SetNoWorkersNeeded(v),
                "Gebäude arbeiten ohne zugewiesene Arbeiter.");
            _noComputing = BuildIgnoreToggle("Kein Computing nötig", () => Svc?.Sandbox?.NoComputingNeeded ?? false,
                v => Svc?.Sandbox?.SetNoComputingNeeded(v),
                "Maschinen laufen ohne ausreichendes Computing.");
            _noUnity = BuildIgnoreToggle("Keine Unity nötig", () => Svc?.Sandbox?.NoUnityNeeded ?? false,
                v => Svc?.Sandbox?.SetNoUnityNeeded(v),
                "Aktionen/Gebäude, die Unity verlangen, laufen ohne Unity.");
            _noFood = BuildIgnoreToggle("Keine Lebensmittel nötig", () => Svc?.Sandbox?.NoFoodNeeded ?? false,
                v => Svc?.Sandbox?.SetNoFoodNeeded(v),
                "Keine Hunger-/Versorgungs-Strafe bei fehlenden Lebensmitteln.");
            _instaBuild = BuildIgnoreToggle("Sofortbau", () => false,
                v => Svc?.Building?.SetInstaBuild(v),
                "Gebäude, Forschung, Upgrades und Reparaturen werden sofort fertig.");
            _noFuel = BuildIgnoreToggle("Kein Treibstoffverbrauch", () => false,
                v => Svc?.FleetVehicle?.SetFuelConsumptionDisabled(v),
                "Fahrzeuge verbrauchen keinen Treibstoff mehr.");
            _noMaintenance = BuildIgnoreToggle("Wartung deaktivieren", () => Svc?.MaintenanceDisabled ?? false,
                v => Svc?.SetMaintenanceDisabled(v),
                "Kein Wartungsverbrauch; vorhandene Schäden werden repariert.");

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Kreativmodus"),
                BuildMasterToggle(),

                CheatWidgets.SectionTitle("Läuft trotz Mangel"),
                _noPower, _noWorkers, _noComputing, _noUnity, _noFood,

                CheatWidgets.SectionTitle("Bau & Betrieb"),
                _instaBuild, _noFuel, _noMaintenance,

                CheatWidgets.SectionTitle("Spielgeschwindigkeit"),
                BuildSpeedButtons(),
                BuildUncappedToggle(),

                CheatWidgets.SectionTitle("Unendlich-Quelle/Senke"),
                BuildSourceSinkToggle(),

                CheatWidgets.SectionTitle("God-Werkzeug (Welt-Klick)"),
                BuildGodWandToggle()
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        private static CheatService Svc => CheatService.Instance;

        // Master: schaltet alle Dauer-Cheats des Kreativmodus auf einmal und zieht die Einzel-Toggles optisch mit.
        private UiComponent BuildMasterToggle()
        {
            return CheatWidgets.NewToggleRow(
                "Kreativmodus (alles auf einmal)",
                false,
                v =>
                {
                    // Backend: alle Flags setzen.
                    Svc?.Sandbox?.SetAllIgnoreMissing(v);
                    Svc?.Building?.SetInstaBuild(v);
                    Svc?.FleetVehicle?.SetFuelConsumptionDisabled(v);
                    Svc?.SetMaintenanceDisabled(v);

                    // UI: Einzel-Toggles mitziehen (ohne ihre Backend-onChanged erneut auszuloesen).
                    _suppress = true;
                    _noPower?.Value(v); _noWorkers?.Value(v); _noComputing?.Value(v);
                    _noUnity?.Value(v); _noFood?.Value(v);
                    _instaBuild?.Value(v); _noFuel?.Value(v); _noMaintenance?.Value(v);
                    _suppress = false;

                    CheatMenuStatus.Show(v ? "Kreativmodus AN" : "Kreativmodus AUS");
                },
                "Aktiviert auf einen Schlag: kein Strom/Arbeiter/Computing/Unity/Lebensmittel nötig, Sofortbau, kein Treibstoff, keine Wartung.");
        }

        // Einzel-Toggle, dessen onChanged waehrend Master-Updates unterdrueckt wird.
        private Toggle BuildIgnoreToggle(string label, System.Func<bool> initial, System.Action<bool> apply, string tooltip)
        {
            return CheatWidgets.NewToggleRow(
                label,
                initial(),
                v => { if (!_suppress) apply(v); },
                tooltip);
        }

        // Geschwindigkeit: 1x (Reset) + 5x/10x/20x.
        private UiComponent BuildSpeedButtons()
        {
            var reset = CheatWidgets.GeneralButton("1x", () => Speed(1), "Zurück auf Normalgeschwindigkeit.");
            var x5  = CheatWidgets.PrimaryButton("5x",  () => Speed(5),  "Simulation 5-fach.");
            var x10 = CheatWidgets.PrimaryButton("10x", () => Speed(10), "Simulation 10-fach.");
            var x20 = CheatWidgets.PrimaryButton("20x", () => Speed(20), "Simulation 20-fach (CPU-abhängig).");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(reset, x5, x10, x20);
            return row;
        }

        private static void Speed(int mult)
        {
            Svc?.GameSpeed?.SetSpeed(mult);
            CheatMenuStatus.Show($"Spielgeschwindigkeit: {mult}x");
        }

        private UiComponent BuildUncappedToggle()
        {
            return CheatWidgets.NewToggleRow(
                "Uncapped (so schnell wie die CPU kann)",
                Svc?.GameSpeed?.Uncapped ?? false,
                v => Svc?.GameSpeed?.SetUncapped(v),
                "Hebt das Sim-Geschwindigkeitslimit auf — die Simulation läuft so schnell, wie der Rechner erlaubt.");
        }

        // Schaltet das eingebaute Unendlich-Quelle/Senke-Cheat-Gebäude in der Bau-Toolbar frei.
        private UiComponent BuildSourceSinkToggle()
        {
            return CheatWidgets.NewToggleRow(
                "In Bau-Toolbar freischalten",
                Svc?.SourceSink?.Enabled ?? false,
                v =>
                {
                    Svc?.SourceSink?.SetEnabled(v);
                    CheatMenuStatus.Show(v
                        ? "Quelle/Senke in der Bau-Toolbar freigeschaltet"
                        : "Quelle/Senke deaktiviert");
                },
                "Schaltet das eingebaute Cheat-Gebäude frei: unendliche Quelle für jedes Produkt + bodenlose Senke. Danach ganz normal über die Bau-Toolbar platzieren.");
        }

        // Aktiviert das Welt-Klick-Werkzeug: Werften/Cargo-Depots/Fahrzeuge per Klick volltanken.
        private UiComponent BuildGodWandToggle()
        {
            return CheatWidgets.NewToggleRow(
                "God-Werkzeug aktiv",
                Svc?.IsGodWandActive ?? false,
                v =>
                {
                    bool ok = Svc?.SetGodWandActive(v) ?? false;
                    CheatMenuStatus.Show(!ok
                        ? "God-Werkzeug nicht verfügbar"
                        : v ? "God-Werkzeug AN — Werft/Depot/Fahrzeug anklicken" : "God-Werkzeug AUS");
                },
                "Solange aktiv: Linksklick auf eine Werft, ein Cargo-Depot oder ein Fahrzeug tankt es sofort voll.");
        }
    }
}
