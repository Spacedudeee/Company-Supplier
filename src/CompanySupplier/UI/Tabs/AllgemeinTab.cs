using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

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

        // Weitere Zustands-Toggles dieses Tabs — Referenzen fuer den zentralen UI-Sync (CheatUiSync).
        private Toggle _master, _uncapped, _sourceSink, _godWand, _diseases, _happiness, _keepUnity;

        // Forschungs-Toggles (Voraussetzungen ignorieren) — ebenfalls per CheatUiSync nachgezogen.
        private Toggle _researchIgnoreItems, _researchIgnoreParents;

        // Unterdrückt die onChanged-Backend-Aufrufe, während der Sync/Master die Toggles optisch setzt.
        private bool _suppress;

        public AllgemeinTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(AllgemeinTab), SyncFromState);
        }

        /// <summary>Zieht alle Toggle-Zustaende dieses Tabs aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                bool noPower     = Svc?.Sandbox?.NoPowerNeeded ?? false;
                bool noWorkers   = Svc?.Sandbox?.NoWorkersNeeded ?? false;
                bool noComputing = Svc?.Sandbox?.NoComputingNeeded ?? false;
                bool noUnity     = Svc?.Sandbox?.NoUnityNeeded ?? false;
                bool noFood      = Svc?.Sandbox?.NoFoodNeeded ?? false;
                bool instaBuild  = Svc?.Building?.InstaBuildEnabled ?? false;
                bool noFuel      = Svc?.FleetVehicle?.FuelConsumptionDisabled ?? false;
                bool noMaint     = Svc?.MaintenanceDisabled ?? false;

                _noPower?.Value(noPower);
                _noWorkers?.Value(noWorkers);
                _noComputing?.Value(noComputing);
                _noUnity?.Value(noUnity);
                _noFood?.Value(noFood);
                _instaBuild?.Value(instaBuild);
                _noFuel?.Value(noFuel);
                _noMaintenance?.Value(noMaint);
                // Master zeigt AN, wenn wirklich alles an ist (sonst wuerde er nach Panik-Aus AN bleiben).
                _master?.Value(noPower && noWorkers && noComputing && noUnity && noFood && instaBuild && noFuel && noMaint);

                _uncapped?.Value(Svc?.GameSpeed?.Uncapped ?? false);
                _sourceSink?.Value(Svc?.SourceSink?.Enabled ?? false);
                _godWand?.Value(Svc?.IsGodWandActive ?? false);
                _diseases?.Value(Svc?.Population?.DiseasesDisabled ?? false);
                _happiness?.Value(Svc?.Population?.MaxConsumptionHappiness ?? false);
                _keepUnity?.Value(Svc?.Population?.KeepUnityFull ?? false);

                _researchIgnoreItems?.Value(Svc?.Research?.IgnoreItemRequirements ?? false);
                _researchIgnoreParents?.Value(Svc?.Research?.IgnoreParentRequirements ?? false);
            }
            finally { _suppress = false; }
        }

        public string Name => L.Tab_Allgemein;

        // "Settlement"-Toolbar-Icon (Allgemein). Verifizierter Const-Pfad aus Mafi.Base.IconsPaths.
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Settlement.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            // Kreativmodus-Einzel-Toggles erst in ihre Felder bauen (Collection-Initializer erlauben keine Zuweisung).
            _noPower = BuildIgnoreToggle(L.Gen_NoPower, () => Svc?.Sandbox?.NoPowerNeeded ?? false,
                v => Svc?.Sandbox?.SetNoPowerNeeded(v),
                L.Gen_NoPowerTip);
            _noWorkers = BuildIgnoreToggle(L.Gen_NoWorkers, () => Svc?.Sandbox?.NoWorkersNeeded ?? false,
                v => Svc?.Sandbox?.SetNoWorkersNeeded(v),
                L.Gen_NoWorkersTip);
            _noComputing = BuildIgnoreToggle(L.Gen_NoComputing, () => Svc?.Sandbox?.NoComputingNeeded ?? false,
                v => Svc?.Sandbox?.SetNoComputingNeeded(v),
                L.Gen_NoComputingTip);
            _noUnity = BuildIgnoreToggle(L.Gen_NoUnity, () => Svc?.Sandbox?.NoUnityNeeded ?? false,
                v => Svc?.Sandbox?.SetNoUnityNeeded(v),
                L.Gen_NoUnityTip);
            _noFood = BuildIgnoreToggle(L.Gen_NoFood, () => Svc?.Sandbox?.NoFoodNeeded ?? false,
                v => Svc?.Sandbox?.SetNoFoodNeeded(v),
                L.Gen_NoFoodTip);
            _instaBuild = BuildIgnoreToggle(L.Gen_InstaBuild, () => Svc?.Building?.InstaBuildEnabled ?? false,
                v => Svc?.Building?.SetInstaBuild(v),
                L.Gen_InstaBuildTip);
            _noFuel = BuildIgnoreToggle(L.Gen_NoFuel, () => Svc?.FleetVehicle?.FuelConsumptionDisabled ?? false,
                v => Svc?.FleetVehicle?.SetFuelConsumptionDisabled(v),
                L.Gen_NoFuelTip);
            _noMaintenance = BuildIgnoreToggle(L.Gen_NoMaintenance, () => Svc?.MaintenanceDisabled ?? false,
                v => Svc?.SetMaintenanceDisabled(v),
                L.Gen_NoMaintenanceTip);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Gen_TitleCreative),
                BuildMasterToggle(),

                CheatWidgets.SectionTitle(L.Gen_TitleDespiteShortage),
                CheatWidgets.ToggleGrid(_noPower, _noWorkers, _noComputing, _noUnity, _noFood),

                CheatWidgets.SectionTitle(L.Gen_TitleBuildOps),
                CheatWidgets.ToggleGrid(_instaBuild, _noFuel, _noMaintenance),

                CheatWidgets.SectionTitle(L.Gen_TitleSpeed),
                BuildSpeedButtons(),
                BuildUncappedToggle(),

                CheatWidgets.SectionTitle(L.Gen_TitleSourceSink),
                BuildSourceSinkToggle(),

                CheatWidgets.SectionTitle(L.Gen_TitleGodTool),
                BuildGodWandToggle(),

                CheatWidgets.SectionTitle(L.Gen_TitlePopulation),
                CheatWidgets.ToggleGrid(BuildDiseasesToggle(), BuildHappinessToggle(), BuildKeepUnityToggle()),
                CheatWidgets.SectionTitle(L.Gen_TitleAddPopulation),
                BuildPopulationStepper(),
                BuildPopulationSetRow(),

                CheatWidgets.SectionTitle(L.Gen_TitleResearch),
                BuildResearchButtons(),
                BuildResearchUnlockButtons(),
                CheatWidgets.ToggleGrid(BuildResearchIgnoreItemsToggle(), BuildResearchIgnoreParentsToggle()),

                CheatWidgets.SectionTitle(L.Gen_TitleAddUnity),
                BuildUnityStepper(),

                CheatWidgets.SectionTitle(L.Gen_TitleWorldgen),
                BuildWorldgenButton()
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
            _master = CheatWidgets.NewToggleRow(
                L.Gen_Master,
                false,
                v =>
                {
                    if (_suppress) return;

                    // Backend: alle Flags setzen.
                    Svc?.Sandbox?.SetAllIgnoreMissing(v);
                    Svc?.Building?.SetInstaBuild(v);
                    Svc?.FleetVehicle?.SetFuelConsumptionDisabled(v);
                    Svc?.SetMaintenanceDisabled(v);

                    // UI: ALLE Toggles (auch die des Fahrzeuge-Tabs) aus dem Backend nachziehen —
                    // der zentrale Sync setzt die Werte mit _suppress-Schutz, ohne onChanged-Backend-Aufrufe.
                    CheatUiSync.SyncAll();

                    CheatMenuStatus.Show(v ? L.Gen_StatusCreativeOn : L.Gen_StatusCreativeOff);
                },
                L.Gen_MasterTip);
            return _master;
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
            var reset = CheatWidgets.GeneralButton("1x", () => Speed(1), L.Gen_SpeedResetTip);
            var x5  = CheatWidgets.PrimaryButton("5x",  () => Speed(5),  L.Gen_Speed5Tip);
            var x10 = CheatWidgets.PrimaryButton("10x", () => Speed(10), L.Gen_Speed10Tip);
            var x20 = CheatWidgets.PrimaryButton("20x", () => Speed(20), L.Gen_Speed20Tip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(reset, x5, x10, x20);
            return row;
        }

        private static void Speed(int mult)
        {
            Svc?.GameSpeed?.SetSpeed(mult);
            CheatMenuStatus.Show(L.Gen_StatusSpeed(mult));
        }

        private UiComponent BuildUncappedToggle()
        {
            _uncapped = CheatWidgets.NewToggleRow(
                L.Gen_Uncapped,
                Svc?.GameSpeed?.Uncapped ?? false,
                v => { if (!_suppress) Svc?.GameSpeed?.SetUncapped(v); },
                L.Gen_UncappedTip);
            return _uncapped;
        }

        // Schaltet das eingebaute Unendlich-Quelle/Senke-Cheat-Gebäude in der Bau-Toolbar frei.
        private UiComponent BuildSourceSinkToggle()
        {
            _sourceSink = CheatWidgets.NewToggleRow(
                L.Gen_SourceSink,
                Svc?.SourceSink?.Enabled ?? false,
                v =>
                {
                    if (_suppress) return;
                    Svc?.SourceSink?.SetEnabled(v);
                    CheatMenuStatus.Show(v
                        ? L.Gen_StatusSourceSinkOn
                        : L.Gen_StatusSourceSinkOff);
                },
                L.Gen_SourceSinkTip);
            return _sourceSink;
        }

        // Aktiviert das Welt-Klick-Werkzeug: Werften/Cargo-Depots/Fahrzeuge per Klick volltanken.
        private UiComponent BuildGodWandToggle()
        {
            _godWand = CheatWidgets.NewToggleRow(
                L.Gen_GodWand,
                Svc?.IsGodWandActive ?? false,
                v =>
                {
                    if (_suppress) return;
                    bool ok = Svc?.SetGodWandActive(v) ?? false;
                    CheatMenuStatus.Show(!ok
                        ? L.Gen_StatusGodWandUnavail
                        : v ? L.Gen_StatusGodWandOn : L.Gen_StatusGodWandOff);
                },
                L.Gen_GodWandTip);
            return _godWand;
        }

        // ------------------------------------------------------------------------------------------
        // Verwaltung (früherer Allgemein-Reiter)
        // ------------------------------------------------------------------------------------------

        // A3: Krankheiten deaktivieren. Status aus Population.DiseasesDisabled.
        private UiComponent BuildDiseasesToggle()
        {
            bool initial = CheatService.Instance?.Population?.DiseasesDisabled ?? false;
            _diseases = CheatWidgets.NewToggleRow(
                L.Gen_Diseases,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Population?.SetDiseasesDisabled(v); },
                L.Gen_DiseasesTip);
            return _diseases;
        }

        // A4: Versorgungs-Zufriedenheit max. Status aus Population.MaxConsumptionHappiness.
        private UiComponent BuildHappinessToggle()
        {
            bool initial = CheatService.Instance?.Population?.MaxConsumptionHappiness ?? false;
            _happiness = CheatWidgets.NewToggleRow(
                L.Gen_Happiness,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Population?.SetMaxConsumptionHappiness(v); },
                L.Gen_HappinessTip);
            return _happiness;
        }

        // Unity taeglich bis zur Kapazitaet auffuellen. Status aus Population.KeepUnityFull.
        private UiComponent BuildKeepUnityToggle()
        {
            bool initial = CheatService.Instance?.Population?.KeepUnityFull ?? false;
            _keepUnity = CheatWidgets.NewToggleRow(
                L.Gen_KeepUnity,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Population?.SetKeepUnityFull(v); },
                L.Gen_KeepUnityTip);
            return _keepUnity;
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

        // Bevölkerung absolut auf einen eingegebenen Wert setzen.
        private UiComponent BuildPopulationSetRow()
        {
            return CheatWidgets.NewIntInputRow(
                L.Gen_SetPopulation,
                v =>
                {
                    CheatService.Instance?.Population?.SetPopulation(v);
                    CheatMenuStatus.Show(L.Gen_StatusPopulationSet(v));
                },
                min: 0);
        }

        // A6 + A7: zwei Forschungs-Buttons nebeneinander.
        private UiComponent BuildResearchButtons()
        {
            var finishCurrent = CheatWidgets.PrimaryButton(
                L.Gen_ResearchFinish,
                () =>
                {
                    CheatService.Instance?.Research?.FinishCurrentResearch();
                    CheatMenuStatus.Show(L.Gen_StatusResearchFinished);
                },
                L.Gen_ResearchFinishTip);

            var unlockAll = CheatWidgets.GeneralButton(
                L.Gen_ResearchUnlockAll,
                () =>
                {
                    CheatService.Instance?.Research?.UnlockAllResearch();
                    CheatMenuStatus.Show(L.Gen_StatusResearchUnlocked);
                },
                L.Gen_ResearchUnlockAllTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(finishCurrent, unlockAll);
            return row;
        }

        // Verfügbare + wiederholbare Forschung freischalten.
        private UiComponent BuildResearchUnlockButtons()
        {
            var available = CheatWidgets.PrimaryButton(
                L.Gen_ResearchUnlockAvailable,
                () =>
                {
                    CheatService.Instance?.Research?.UnlockAvailableResearch();
                    CheatMenuStatus.Show(L.Gen_StatusResearchUnlocked);
                },
                L.Gen_ResearchUnlockAvailableTip);

            var repeatable = CheatWidgets.PrimaryButton(
                L.Gen_ResearchUnlockRepeatable,
                () =>
                {
                    CheatService.Instance?.Research?.UnlockRepeatableResearch();
                    CheatMenuStatus.Show(L.Gen_StatusResearchUnlocked);
                },
                L.Gen_ResearchUnlockRepeatableTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(available, repeatable);
            return row;
        }

        // Forschung ignoriert benötigte Produkte/Bedingungen. Status aus Research.IgnoreItemRequirements.
        private UiComponent BuildResearchIgnoreItemsToggle()
        {
            bool initial = CheatService.Instance?.Research?.IgnoreItemRequirements ?? false;
            _researchIgnoreItems = CheatWidgets.NewToggleRow(
                L.Gen_ResearchIgnoreItems,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Research?.SetIgnoreItemRequirements(v); },
                L.Gen_ResearchIgnoreItemsTip);
            return _researchIgnoreItems;
        }

        // Jeder Knoten ist ohne Vorgänger forschbar. Status aus Research.IgnoreParentRequirements.
        private UiComponent BuildResearchIgnoreParentsToggle()
        {
            bool initial = CheatService.Instance?.Research?.IgnoreParentRequirements ?? false;
            _researchIgnoreParents = CheatWidgets.NewToggleRow(
                L.Gen_ResearchIgnoreParents,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Research?.SetIgnoreParentRequirements(v); },
                L.Gen_ResearchIgnoreParentsTip);
            return _researchIgnoreParents;
        }

        // WorldGen-Fixes: gesperrtes Saatgut/Radar freischalten.
        private UiComponent BuildWorldgenButton()
        {
            return CheatWidgets.PrimaryButton(
                L.Gen_WorldgenUnlock,
                () =>
                {
                    CheatService.Instance?.Research?.UnlockWorldgenFixes();
                    CheatMenuStatus.Show(L.Gen_StatusWorldgen);
                },
                L.Gen_WorldgenUnlockTip);
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
