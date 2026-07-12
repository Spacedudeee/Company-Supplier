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
    /// Reiter "Umwelt": deaktiviert die Verschmutzung. Ein Master-Schalter schaltet alle sechs Quellen
    /// (Luft, Wasser, Deponie, Fahrzeuge, Schiffe, Züge) auf einmal aus; darunter Einzelschalter zur
    /// Feinsteuerung. Bestehende Verschmutzung baut sich danach von selbst ab.
    ///
    /// Bindet ueber <c>CheatService.Instance</c>. Registrierung per <c>[GlobalDependency(AsEverything)]</c>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class UmweltTab : ICheatTab
    {
        private readonly UiComponent _content;

        private Toggle _air, _water, _landfill, _vehicles, _ships, _trains;
        private Toggle _master;
        private bool _suppress;

        public UmweltTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(UmweltTab), SyncFromState);
        }

        /// <summary>Zieht alle Toggle-Zustaende dieses Tabs aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                bool air = Svc?.Pollution?.AirDisabled ?? false;
                bool water = Svc?.Pollution?.WaterDisabled ?? false;
                bool landfill = Svc?.Pollution?.LandfillDisabled ?? false;
                bool vehicles = Svc?.Pollution?.VehiclesDisabled ?? false;
                bool ships = Svc?.Pollution?.ShipsDisabled ?? false;
                bool trains = Svc?.Pollution?.TrainsDisabled ?? false;

                _air?.Value(air); _water?.Value(water); _landfill?.Value(landfill);
                _vehicles?.Value(vehicles); _ships?.Value(ships); _trains?.Value(trains);
                _master?.Value(air && water && landfill && vehicles && ships && trains);
            }
            finally { _suppress = false; }
        }

        public string Name => L.Tab_Umwelt;

        // Waste-Toolbar-Icon (Abfall/Verschmutzung) — passt zum Umwelt-/Verschmutzungs-Tab.
        // Verifizierter Pfad aus Mafi.Base.IconsPaths.ToolbarWaste.
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Waste.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            _air = BuildToggle(L.Umw_Air, () => Svc?.Pollution?.AirDisabled ?? false,
                v => Svc?.Pollution?.SetAirDisabled(v), L.Umw_AirTip);
            _water = BuildToggle(L.Umw_Water, () => Svc?.Pollution?.WaterDisabled ?? false,
                v => Svc?.Pollution?.SetWaterDisabled(v), L.Umw_WaterTip);
            _landfill = BuildToggle(L.Umw_Landfill, () => Svc?.Pollution?.LandfillDisabled ?? false,
                v => Svc?.Pollution?.SetLandfillDisabled(v), L.Umw_LandfillTip);
            _vehicles = BuildToggle(L.Umw_Vehicles, () => Svc?.Pollution?.VehiclesDisabled ?? false,
                v => Svc?.Pollution?.SetVehiclesDisabled(v), L.Umw_VehiclesTip);
            _ships = BuildToggle(L.Umw_Ships, () => Svc?.Pollution?.ShipsDisabled ?? false,
                v => Svc?.Pollution?.SetShipsDisabled(v), L.Umw_ShipsTip);
            _trains = BuildToggle(L.Umw_Trains, () => Svc?.Pollution?.TrainsDisabled ?? false,
                v => Svc?.Pollution?.SetTrainsDisabled(v), L.Umw_TrainsTip);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Umw_TitlePollution),
                BuildMasterToggle(),

                CheatWidgets.SectionTitle(L.Umw_TitleSources),
                CheatWidgets.ToggleGrid(_air, _water, _landfill, _vehicles, _ships, _trains)
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        private UiComponent BuildMasterToggle()
        {
            _master = CheatWidgets.NewToggleRow(
                L.Umw_Master,
                false,
                v =>
                {
                    if (_suppress) return;
                    Svc?.Pollution?.SetAllDisabled(v);
                    // Alle Toggles (inkl. Master selbst) aus dem Backend nachziehen — suppress-geschuetzt.
                    CheatUiSync.SyncAll();
                    CheatMenuStatus.Show(v ? L.Umw_StatusOff : L.Umw_StatusNormal);
                },
                L.Umw_MasterTip);
            return _master;
        }

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
