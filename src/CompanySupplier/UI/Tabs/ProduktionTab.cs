using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Produktion": Dauer-Boosts ueber den <see cref="Cheats.BoostCheats"/>-Provider —
    /// Foerderleistung/Farm-Ertrag/Solar-Ertrag ×10, alle Maschinen zwangslaufen, unerschoepfliches
    /// Grundwasser. Alles Toggles; der zentrale UI-Sync (CheatUiSync) zieht sie nach Panik-Aus/Preset-Laden nach.
    ///
    /// Bindet ueber <c>CheatService.Instance</c>. Registrierung per <c>[GlobalDependency(AsEverything)]</c>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class ProduktionTab : ICheatTab
    {
        private readonly UiComponent _content;

        private Toggle _mining, _farm, _solar, _rainYield, _unlimitedFertility, _forceRun, _machineLowPower, _machineLowComputing, _logisticsPower, _water, _noOilDrain, _noFarmWater, _pipeSlopes;
        private bool _suppress;

        public ProduktionTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(ProduktionTab), SyncFromState);
        }

        /// <summary>Zieht alle Toggle-Zustaende dieses Tabs aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                _mining?.Value(Svc?.Boost?.MiningBoost ?? false);
                _farm?.Value(Svc?.Boost?.FarmBoost ?? false);
                _solar?.Value(Svc?.Boost?.SolarBoost ?? false);
                _rainYield?.Value(Svc?.Gameplay?.RainYieldBoost ?? false);
                _unlimitedFertility?.Value(Svc?.Terrain?.UnlimitedFertility ?? false);
                _forceRun?.Value(Svc?.Boost?.ForceRunMachines ?? false);
                _machineLowPower?.Value(Svc?.Gameplay?.MachineFullOnLowPower ?? false);
                _machineLowComputing?.Value(Svc?.Gameplay?.MachineFullOnLowComputing ?? false);
                _logisticsPower?.Value(Svc?.Gameplay?.LogisticsIgnorePower ?? false);
                _water?.Value(Svc?.Boost?.UnlimitedWater ?? false);
                _noOilDrain?.Value(Svc?.Boost?.NoOilDrain ?? false);
                _noFarmWater?.Value(Svc?.Gameplay?.NoFarmWater ?? false);
                _pipeSlopes?.Value(HarmonyIntegration.PipeCheats.BuildAlongSlopes);
            }
            finally { _suppress = false; }
        }

        public string Name => L.Tab_Produktion;

        // "Manufacturing"-Toolbar-Icon; fehlt das Asset -> Tab rendert trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Manufacturing.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            _mining   = BuildToggle(L.Prod_Mining,          () => Svc?.Boost?.MiningBoost ?? false,       v => Svc?.Boost?.SetMiningBoost(v),      L.Prod_MiningTip);
            _farm     = BuildToggle(L.Prod_Farm,            () => Svc?.Boost?.FarmBoost ?? false,         v => Svc?.Boost?.SetFarmBoost(v),        L.Prod_FarmTip);
            _solar    = BuildToggle(L.Prod_Solar,           () => Svc?.Boost?.SolarBoost ?? false,        v => Svc?.Boost?.SetSolarBoost(v),       L.Prod_SolarTip);
            _rainYield          = BuildToggle(L.Prod_RainYield,           () => Svc?.Gameplay?.RainYieldBoost ?? false,  v => Svc?.Gameplay?.SetRainYieldBoost(v),   L.Prod_RainYieldTip);
            _unlimitedFertility = BuildToggle(L.Prod_UnlimitedFertility,  () => Svc?.Terrain?.UnlimitedFertility ?? false, v => Svc?.Terrain?.SetUnlimitedFertility(v), L.Prod_UnlimitedFertilityTip);
            _forceRun = BuildToggle(L.Prod_ForceRun,        () => Svc?.Boost?.ForceRunMachines ?? false,  v => Svc?.Boost?.SetForceRunMachines(v), L.Prod_ForceRunTip);
            _machineLowPower     = BuildToggle(L.Prod_MachineLowPower,     () => Svc?.Gameplay?.MachineFullOnLowPower ?? false,     v => Svc?.Gameplay?.SetMachineFullOnLowPower(v),     L.Prod_MachineLowPowerTip);
            _machineLowComputing = BuildToggle(L.Prod_MachineLowComputing, () => Svc?.Gameplay?.MachineFullOnLowComputing ?? false, v => Svc?.Gameplay?.SetMachineFullOnLowComputing(v), L.Prod_MachineLowComputingTip);
            _logisticsPower      = BuildToggle(L.Prod_LogisticsPower,      () => Svc?.Gameplay?.LogisticsIgnorePower ?? false,      v => Svc?.Gameplay?.SetLogisticsIgnorePower(v),      L.Prod_LogisticsPowerTip);
            _water    = BuildToggle(L.Prod_UnlimitedWater,  () => Svc?.Boost?.UnlimitedWater ?? false,    v => Svc?.Boost?.SetUnlimitedWater(v),   L.Prod_UnlimitedWaterTip);
            _noOilDrain = BuildToggle(L.Prod_NoOilDrain,    () => Svc?.Boost?.NoOilDrain ?? false,        v => Svc?.Boost?.SetNoOilDrain(v),       L.Prod_NoOilDrainTip);
            _noFarmWater = BuildToggle(L.Prod_NoFarmWater,  () => Svc?.Gameplay?.NoFarmWater ?? false,    v => Svc?.Gameplay?.SetNoFarmWater(v),   L.Prod_NoFarmWaterTip);
            // Pipe-Cheat (Harmony): statischer Schalter, den der InitPathFinding-Patch liest.
            _pipeSlopes = BuildToggle(L.Prod_PipeSlopes,    () => HarmonyIntegration.PipeCheats.BuildAlongSlopes, v => HarmonyIntegration.PipeCheats.BuildAlongSlopes = v, L.Prod_PipeSlopesTip);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Prod_TitleYield),
                CheatWidgets.ToggleGrid(_mining, _farm, _solar, _rainYield, _unlimitedFertility),

                CheatWidgets.SectionTitle(L.Prod_TitleMachines),
                CheatWidgets.ToggleGrid(_forceRun, _machineLowPower, _machineLowComputing, _logisticsPower),

                CheatWidgets.SectionTitle(L.Prod_TitleReserves),
                CheatWidgets.ToggleGrid(_water, _noOilDrain, _noFarmWater),

                CheatWidgets.SectionTitle(L.Prod_TitlePipes),
                _pipeSlopes
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // Einzel-Toggle, dessen onChanged waehrend Sync/Preset-Updates unterdrueckt wird (Name != Typ 'Toggle').
        private Toggle BuildToggle(string label, Func<bool> initial, Action<bool> apply, string tooltip)
        {
            return CheatWidgets.NewToggleRow(
                label,
                initial(),
                v => { if (!_suppress) apply(v); },
                tooltip);
        }
    }
}
