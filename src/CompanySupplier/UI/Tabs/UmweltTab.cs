using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

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

        public string Name => "Umwelt";

        // Waste-Toolbar-Icon (Abfall/Verschmutzung) — passt zum Umwelt-/Verschmutzungs-Tab.
        // Verifizierter Pfad aus Mafi.Base.IconsPaths.ToolbarWaste.
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Waste.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            _air = BuildToggle("Luftverschmutzung aus", () => Svc?.Pollution?.AirDisabled ?? false,
                v => Svc?.Pollution?.SetAirDisabled(v), "Fabriken/Generatoren stoßen keine Luftverschmutzung mehr aus.");
            _water = BuildToggle("Wasserverschmutzung aus", () => Svc?.Pollution?.WaterDisabled ?? false,
                v => Svc?.Pollution?.SetWaterDisabled(v), "Keine Wasserverschmutzung mehr.");
            _landfill = BuildToggle("Deponie-Verschmutzung aus", () => Svc?.Pollution?.LandfillDisabled ?? false,
                v => Svc?.Pollution?.SetLandfillDisabled(v), "Deponien verschmutzen die Umgebung nicht mehr.");
            _vehicles = BuildToggle("Fahrzeug-Abgase aus", () => Svc?.Pollution?.VehiclesDisabled ?? false,
                v => Svc?.Pollution?.SetVehiclesDisabled(v), "Fahrzeuge stoßen keine Abgase mehr aus.");
            _ships = BuildToggle("Schiffs-Abgase aus", () => Svc?.Pollution?.ShipsDisabled ?? false,
                v => Svc?.Pollution?.SetShipsDisabled(v), "Schiffe stoßen keine Abgase mehr aus.");
            _trains = BuildToggle("Zug-Abgase aus", () => Svc?.Pollution?.TrainsDisabled ?? false,
                v => Svc?.Pollution?.SetTrainsDisabled(v), "Züge stoßen keine Abgase mehr aus.");

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Verschmutzung"),
                BuildMasterToggle(),

                CheatWidgets.SectionTitle("Einzelne Quellen"),
                _air, _water, _landfill, _vehicles, _ships, _trains
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        private UiComponent BuildMasterToggle()
        {
            _master = CheatWidgets.NewToggleRow(
                "Keine Verschmutzung (alles aus)",
                false,
                v =>
                {
                    if (_suppress) return;
                    Svc?.Pollution?.SetAllDisabled(v);
                    // Alle Toggles (inkl. Master selbst) aus dem Backend nachziehen — suppress-geschuetzt.
                    CheatUiSync.SyncAll();
                    CheatMenuStatus.Show(v ? "Verschmutzung AUS" : "Verschmutzung normal");
                },
                "Schaltet alle sechs Verschmutzungsquellen auf einmal ab. Bestehende Verschmutzung baut sich danach selbst ab.");
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
