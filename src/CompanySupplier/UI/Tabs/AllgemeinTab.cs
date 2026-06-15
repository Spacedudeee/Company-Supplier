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
    /// Reiter "Allgemein" (ui-spec A1–A8): Wartung, Bau, Forschung, Bevoelkerung, Unity, Lebensmittel.
    /// - A1 Toggle Sofortbau                  -> Building.SetInstaBuild(bool)        [kein Status-Prop -> lokal]
    /// - A2 Toggle Wartung deaktivieren       -> CheatService.SetMaintenanceDisabled (Status .MaintenanceDisabled)
    /// - A3 Toggle Krankheiten deaktivieren   -> Population.SetDiseasesDisabled       (Status .DiseasesDisabled)
    /// - A4 Toggle Versorgungs-Zufriedenheit  -> Population.SetMaxConsumptionHappiness(Status .MaxConsumptionHappiness)
    /// - A5 Stepper Bevoelkerung ±5/±25/±50   -> Population.AddPopulation(int)
    /// - A6 Button Forschung abschliessen     -> Research.FinishCurrentResearch()
    /// - A7 Button Alle Forschung freischalten-> Research.UnlockAllResearch()
    /// - A8 Stepper Unity ±5/±25/±100         -> Population.AddUnity(int)
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class AllgemeinTab : ICheatTab
    {
        private readonly UiComponent _content;

        // A1 "Sofortbau" hat keine Backend-Status-Property -> lokal gehalten.
        private bool _instaBuild;

        public AllgemeinTab()
        {
            _content = BuildContent();
        }

        public string Name => "Allgemein";

        // "Settlement"-Toolbar-Icon (Allgemein). Verifizierter Const-Pfad aus Mafi.Base.IconsPaths
        // (Mono.Cecil-Metadaten). String-Pfad ist in 0.8.5.0 die robuste Variante (kein IconStyle
        // mehr); fehlt das Asset, rendert der Tab trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Settlement.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Bau & Wartung"),
                BuildInstaBuildToggle(),       // A1
                BuildMaintenanceToggle(),      // A2

                CheatWidgets.SectionTitle("Bevölkerung & Versorgung"),
                BuildDiseasesToggle(),         // A3
                BuildHappinessToggle(),        // A4
                CheatWidgets.SectionTitle("Bevölkerung hinzufügen"),
                BuildPopulationStepper(),      // A5

                CheatWidgets.SectionTitle("Forschung"),
                BuildResearchButtons(),        // A6 + A7

                CheatWidgets.SectionTitle("Unity hinzufügen"),
                BuildUnityStepper()            // A8
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // A1: Sofortbau (Gebaeude/Forschung/Upgrades/Fahrzeugbau & Reparatur sofort fertig).
        private UiComponent BuildInstaBuildToggle()
        {
            return CheatWidgets.NewToggleRow(
                "Sofortbau (Instant Build)",
                _instaBuild,
                v =>
                {
                    _instaBuild = v;
                    CheatService.Instance?.Building?.SetInstaBuild(v);
                },
                "Gebäude, Forschung, Upgrades, Fahrzeugbau und Reparaturen werden sofort fertig.");
        }

        // A2: Wartung deaktivieren. Label eindeutig: aktiv = Wartung AUS. Status aus CheatService.
        private UiComponent BuildMaintenanceToggle()
        {
            bool initial = CheatService.Instance?.MaintenanceDisabled ?? false;
            return CheatWidgets.NewToggleRow(
                "Wartung deaktivieren (aktiv = AUS)",
                initial,
                v => CheatService.Instance?.SetMaintenanceDisabled(v),
                "Aktiv = kein Wartungsverbrauch mehr; vorhandene Schäden werden zusätzlich repariert.");
        }

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

        // A5: Bevoelkerung-Stepper ±5/±25/±50 -> Population.AddPopulation(int).
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
