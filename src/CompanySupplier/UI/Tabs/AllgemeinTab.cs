using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Allgemein" — zusammengelegt aus dem früheren Allgemein- UND Sandbox-Reiter:
    /// Kreativmodus (Master + "läuft trotz Mangel"), Bau & Betrieb, Spielgeschwindigkeit, Unendlich-Quelle/Senke,
    /// God-Werkzeug — plus die klassischen Verwaltungs-Cheats (Bevölkerung, Forschung, Unity).
    /// Sofortbau + Wartung stehen nur EINMAL (im "Bau & Betrieb"-Block), da sie sich früher zwischen beiden
    /// Reitern doppelten.
    ///
    /// Bindet über <c>CheatService.Instance</c>. Registrierung per <c>[GlobalDependency(AsEverything)]</c>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class AllgemeinTab : ICheatTab
    {
        private readonly UiComponent _content;

        // Einzel-Toggles des Kreativmodus, damit der Master-Schalter sie auch optisch mitzieht.
        private Toggle _noPower, _noWorkers, _noComputing, _noUnity, _noFood, _instaBuild, _noFuel, _noMaintenance;

        // Unterdrückt die onChanged-Backend-Aufrufe der Kinder, während der Master sie optisch setzt.
        private bool _suppress;

        public AllgemeinTab()
        {
            _content = BuildContent();
        }

        public string Name => "Allgemein";

        // "Settlement"-Toolbar-Icon (Allgemein). Verifizierter Const-Pfad aus Mafi.Base.IconsPaths.
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Settlement.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            // Kreativmodus-Einzel-Toggles erst in ihre Felder bauen (Collection-Initializer erlauben keine Zuweisung).
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
                BuildGodWandToggle(),

                CheatWidgets.SectionTitle("Bevölkerung & Versorgung"),
                BuildDiseasesToggle(),
                BuildHappinessToggle(),
                CheatWidgets.SectionTitle("Bevölkerung hinzufügen"),
                BuildPopulationStepper(),

                CheatWidgets.SectionTitle("Forschung"),
                BuildResearchButtons(),

                CheatWidgets.SectionTitle("Unity hinzufügen"),
                BuildUnityStepper()
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // ------------------------------------------------------------------------------------------
        // Kreativmodus (früherer Sandbox-Reiter)
        // ------------------------------------------------------------------------------------------

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

                    // UI: Einzel-Toggles mitziehen (ohne ihre Backend-onChanged erneut auszulösen).
                    _suppress = true;
                    _noPower?.Value(v); _noWorkers?.Value(v); _noComputing?.Value(v);
                    _noUnity?.Value(v); _noFood?.Value(v);
                    _instaBuild?.Value(v); _noFuel?.Value(v); _noMaintenance?.Value(v);
                    _suppress = false;

                    CheatMenuStatus.Show(v ? "Kreativmodus AN" : "Kreativmodus AUS");
                },
                "Aktiviert auf einen Schlag: kein Strom/Arbeiter/Computing/Unity/Lebensmittel nötig, Sofortbau, kein Treibstoff, keine Wartung.");
        }

        // Einzel-Toggle, dessen onChanged während Master-Updates unterdrückt wird.
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

        // ------------------------------------------------------------------------------------------
        // Verwaltung (früherer Allgemein-Reiter)
        // ------------------------------------------------------------------------------------------

        // A3: Krankheiten deaktivieren. Status aus Population.DiseasesDisabled.
        private UiComponent BuildDiseasesToggle()
        {
            bool initial = CheatService.Instance?.Population?.DiseasesDisabled ?? false;
            return CheatWidgets.NewToggleRow(
                "Krankheiten deaktivieren",
                initial,
                v => CheatService.Instance?.Population?.SetDiseasesDisabled(v),
                "Jede neu auftretende Seuche wird automatisch sofort beendet.");
        }

        // A4: Versorgungs-Zufriedenheit max. Status aus Population.MaxConsumptionHappiness.
        private UiComponent BuildHappinessToggle()
        {
            bool initial = CheatService.Instance?.Population?.MaxConsumptionHappiness ?? false;
            return CheatWidgets.NewToggleRow(
                "Versorgungs-Zufriedenheit max.",
                initial,
                v => CheatService.Instance?.Population?.SetMaxConsumptionHappiness(v),
                "Hält die Siedlungs-Zufriedenheit aus Versorgung/Lebensmitteln dauerhaft auf Maximum.");
        }

        // A5: Bevölkerung-Stepper ±5/±25/±50 -> Population.AddPopulation(int).
        private UiComponent BuildPopulationStepper()
        {
            var steps = new Dictionary<int, Action<int>>
            {
                { 5,  d => CheatService.Instance?.Population?.AddPopulation(d) },
                { 25, d => CheatService.Instance?.Population?.AddPopulation(d) },
                { 50, d => CheatService.Instance?.Population?.AddPopulation(d) },
            };
            return CheatWidgets.NewIncrementButtonGroup(steps);
        }

        // A6 + A7: zwei Forschungs-Buttons nebeneinander.
        private UiComponent BuildResearchButtons()
        {
            var finishCurrent = CheatWidgets.PrimaryButton(
                "Aktuelle Forschung abschließen",
                () =>
                {
                    CheatService.Instance?.Research?.FinishCurrentResearch();
                    CheatMenuStatus.Show("Aktuelle Forschung abgeschlossen");
                },
                "Beendet die aktuell laufende Forschung sofort.");

            var unlockAll = CheatWidgets.GeneralButton(
                "Alle Forschung freischalten",
                () =>
                {
                    CheatService.Instance?.Research?.UnlockAllResearch();
                    CheatMenuStatus.Show("Kompletter Forschungsbaum freigeschaltet");
                },
                "Schaltet den kompletten Forschungsbaum frei.");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(finishCurrent, unlockAll);
            return row;
        }

        // A8: Unity-Stepper ±5/±25/±100 -> Population.AddUnity(int).
        private UiComponent BuildUnityStepper()
        {
            var steps = new Dictionary<int, Action<int>>
            {
                { 5,   d => CheatService.Instance?.Population?.AddUnity(d) },
                { 25,  d => CheatService.Instance?.Population?.AddUnity(d) },
                { 100, d => CheatService.Instance?.Population?.AddUnity(d) },
            };
            return CheatWidgets.NewIncrementButtonGroup(steps);
        }
    }
}
