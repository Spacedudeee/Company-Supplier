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
        private bool _suppress;

        public UmweltTab()
        {
            _content = BuildContent();
        }

        public string Name => "Umwelt";

        public string IconPath => "";

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
            return CheatWidgets.NewToggleRow(
                "Keine Verschmutzung (alles aus)",
                false,
                v =>
                {
                    Svc?.Pollution?.SetAllDisabled(v);
                    _suppress = true;
                    _air?.Value(v); _water?.Value(v); _landfill?.Value(v);
                    _vehicles?.Value(v); _ships?.Value(v); _trains?.Value(v);
                    _suppress = false;
                    CheatMenuStatus.Show(v ? "Verschmutzung AUS" : "Verschmutzung normal");
                },
                "Schaltet alle sechs Verschmutzungsquellen auf einmal ab. Bestehende Verschmutzung baut sich danach selbst ab.");
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
