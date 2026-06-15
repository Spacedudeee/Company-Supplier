using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Werft &amp; Flotte" (ui-spec W1–W3): Sofort-Aktionen fuer das Welt-Schiff (Flotte).
    /// Drei reine Aktions-Buttons (kein Status):
    /// - W1 Schiff zwangsentladen      -> FleetVehicle.ForceUnloadShip()
    /// - W2 Exploration abschliessen    -> FleetVehicle.FinishExploration()
    /// - W3 Schiff reparieren           -> FleetVehicle.RepairShip()
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class WerftFlotteTab : ICheatTab
    {
        private readonly UiComponent _content;

        public WerftFlotteTab()
        {
            _content = BuildContent();
        }

        public string Name => "Werft & Flotte";

        // "CargoShip"-Toolbar-Icon (Werft/Flotte). Verifizierter Const-Pfad aus Mafi.Base.IconsPaths
        // (ToolbarTrade → .../Toolbar/CargoShip.svg). String-Pfad ist in 0.8.5.0 die robuste Variante
        // (kein IconStyle mehr); fehlt das Asset, rendert der Tab trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/CargoShip.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Welt-Schiff (Flotte)"),
                BuildForceUnloadButton(),   // W1
                BuildFinishExplorationButton(), // W2
                BuildRepairButton()         // W3
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // W1: Gesamte Fracht des Welt-Schiffs sofort in die zugewiesene Werft entladen.
        private UiComponent BuildForceUnloadButton()
        {
            return CheatWidgets.PrimaryButton(
                "Schiff zwangsentladen",
                () => CheatService.Instance?.FleetVehicle?.ForceUnloadShip(),
                "Entlaedt die gesamte Fracht des Welt-Schiffs sofort in seine zugewiesene Werft.");
        }

        // W2: Laufende Erkundung der aktuellen Karten-Position sofort beenden.
        private UiComponent BuildFinishExplorationButton()
        {
            return CheatWidgets.PrimaryButton(
                "Exploration abschließen",
                () => CheatService.Instance?.FleetVehicle?.FinishExploration(),
                "Beendet die laufende Erkundung der aktuellen Karten-Position sofort.");
        }

        // W3: Welt-Schiff sofort auf volle HP reparieren.
        private UiComponent BuildRepairButton()
        {
            return CheatWidgets.PrimaryButton(
                "Schiff reparieren",
                () =>
                {
                    CheatService.Instance?.FleetVehicle?.RepairShip();
                    CheatMenuStatus.Show("Schiff repariert");
                },
                "Repariert das Welt-Schiff (Flotte) sofort auf volle Lebenspunkte.");
        }
    }
}
