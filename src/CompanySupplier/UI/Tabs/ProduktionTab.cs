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

        private Toggle _mining, _farm, _solar, _forceRun, _water;
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
                _forceRun?.Value(Svc?.Boost?.ForceRunMachines ?? false);
                _water?.Value(Svc?.Boost?.UnlimitedWater ?? false);
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
            _forceRun = BuildToggle(L.Prod_ForceRun,        () => Svc?.Boost?.ForceRunMachines ?? false,  v => Svc?.Boost?.SetForceRunMachines(v), L.Prod_ForceRunTip);
            _water    = BuildToggle(L.Prod_UnlimitedWater,  () => Svc?.Boost?.UnlimitedWater ?? false,    v => Svc?.Boost?.SetUnlimitedWater(v),   L.Prod_UnlimitedWaterTip);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Prod_TitleYield),
                CheatWidgets.ToggleGrid(_mining, _farm, _solar),

                CheatWidgets.SectionTitle(L.Prod_TitleMachines),
                _forceRun,

                CheatWidgets.SectionTitle(L.Prod_TitleReserves),
                _water
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
