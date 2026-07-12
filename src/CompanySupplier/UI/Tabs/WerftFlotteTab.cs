using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Localization;
using Mafi.Core.Prototypes;                        // IProtoWithIcon
using Mafi.Core.Buildings.Cargo.Ships;             // CargoShipProto
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Werft &amp; Flotte": Sofort-Aktionen fuer das Welt-Schiff (Flotte) plus ein Schiffs-Block:
    /// - Welt-Schiff: zwangsentladen / Exploration abschliessen / reparieren (One-Shot-Buttons).
    /// - Schiffe: globaler Treibstoff-Hebel (<see cref="CheatService.Ships"/>).
    /// - Frachtschiffe pro Typ: Kapazitaet (×2/×3/×5 via <c>ShipCheats</c>) + Geschwindigkeit (via
    ///   <c>VehicleStats</c> — Frachtschiffe sind <c>DrivingEntityProto</c>).
    ///
    /// Bindet ueber <c>CheatService.Instance</c>. Registrierung per <c>[GlobalDependency(AsEverything)]</c>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class WerftFlotteTab : ICheatTab
    {
        private readonly UiComponent _content;

        // Schiffs-Block: Treibstoff-Toggle (zentral gesynct) + gewaehltes Frachtschiff + Info-Label.
        private Toggle _fuelToggle;
        private bool _suppress;
        private CargoShipProto _shipSelected;
        private Label _shipInfo;
        private IReadOnlyList<CargoShipProto> _cargoShips = Array.Empty<CargoShipProto>();

        public WerftFlotteTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(WerftFlotteTab), SyncFromState);
        }

        /// <summary>Zieht den Treibstoff-Toggle + die Frachtschiff-Info aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                _fuelToggle?.Value(CheatService.Instance?.Ships?.ShipsFuelDisabled ?? false);
                RefreshShipInfo();
            }
            finally { _suppress = false; }
        }

        public string Name => L.Tab_WerftFlotte;

        // "CargoShip"-Toolbar-Icon (Werft/Flotte). Verifizierter Const-Pfad aus Mafi.Base.IconsPaths.
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/CargoShip.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Wrf_TitleShip),
                BuildForceUnloadButton(),
                BuildFinishExplorationButton(),
                BuildRepairButton(),

                CheatWidgets.SectionTitle(L.Wrf_TitleShips),
                BuildShipFuelToggle(),

                CheatWidgets.SectionTitle(L.Wrf_TitleCargo),
                BuildCargoShipSection()
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // Gesamte Fracht des Welt-Schiffs sofort in die zugewiesene Werft entladen.
        private UiComponent BuildForceUnloadButton()
            => CheatWidgets.PrimaryButton(L.Wrf_ForceUnload,
                () => CheatService.Instance?.FleetVehicle?.ForceUnloadShip(),
                L.Wrf_ForceUnloadTip);

        // Laufende Erkundung der aktuellen Karten-Position sofort beenden.
        private UiComponent BuildFinishExplorationButton()
            => CheatWidgets.PrimaryButton(L.Wrf_FinishExploration,
                () => CheatService.Instance?.FleetVehicle?.FinishExploration(),
                L.Wrf_FinishExplorationTip);

        // Welt-Schiff sofort auf volle HP reparieren.
        private UiComponent BuildRepairButton()
            => CheatWidgets.PrimaryButton(L.Wrf_Repair,
                () =>
                {
                    CheatService.Instance?.FleetVehicle?.RepairShip();
                    CheatMenuStatus.Show(L.Wrf_StatusRepaired);
                },
                L.Wrf_RepairTip);

        // Globaler Schiffs-Treibstoff-Hebel (ShipsFuelConsumptionMultiplier @ -100%).
        private UiComponent BuildShipFuelToggle()
        {
            _fuelToggle = CheatWidgets.NewToggleRow(
                L.Wrf_ShipsFuel,
                CheatService.Instance?.Ships?.ShipsFuelDisabled ?? false,
                v => { if (!_suppress) CheatService.Instance?.Ships?.SetShipsFuelDisabled(v); },
                L.Wrf_ShipsFuelTip);
            return _fuelToggle;
        }

        // Frachtschiff-Kapazitaet (×2/×3/×5) + Geschwindigkeit pro Typ. Ohne Frachtschiffe -> Hinweis.
        private UiComponent BuildCargoShipSection()
        {
            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();

            var ships = CheatService.Instance?.Ships;
            _cargoShips = ships?.GetCargoShips()
                              .OrderBy(p => CheatWidgets.ProtoDisplayName(p))
                              .ToList()
                          ?? (IReadOnlyList<CargoShipProto>)Array.Empty<CargoShipProto>();

            if (ships == null || _cargoShips.Count == 0)
            {
                col.SetChildren(new Label(new LocStrFormatted(L.Wrf_NoShips)));
                return col;
            }

            _shipSelected = _cargoShips[0];

            Dropdown<CargoShipProto>.OptionFactory factory =
                (CargoShipProto proto, int index, bool isInDropdown) =>
                    new ButtonIconText(Button.None, (IProtoWithIcon)proto, CheatWidgets.ProtoDisplayLabel(proto));

            var dropdown = new Dropdown<CargoShipProto>(factory, null, null, false);
            dropdown.Label(new LocStrFormatted(L.Wrf_CargoShip));
            dropdown.SetOptions(_cargoShips);
            dropdown.OnValueChanged((CargoShipProto proto, int idx) => { _shipSelected = proto; RefreshShipInfo(); });
            dropdown.FlexGrow(1f);
            dropdown.SetValueIndex(0, notifyChangeListeners: false);

            _shipInfo = new Label(new LocStrFormatted(ShipInfoText()));

            // Kapazitaet: ×2/×3/×5 (gruen) + Zuruecksetzen (rot). Setzt CargoShipProto.CapacityMultiplier
            // auf das N-fache des Originals; Reset stellt Kapazitaet + Geschwindigkeit wieder her.
            var x2 = new ButtonText(Button.Primary, new LocStrFormatted("×2"), () => ApplyCapacity(2));
            var x3 = new ButtonText(Button.Primary, new LocStrFormatted("×3"), () => ApplyCapacity(3));
            var x5 = new ButtonText(Button.Primary, new LocStrFormatted("×5"), () => ApplyCapacity(5));
            var reset = CheatWidgets.DangerButton(
                L.Common_Reset,
                () =>
                {
                    if (_shipSelected == null) return;
                    CheatService.Instance?.Ships?.ResetCapacity(_shipSelected);
                    CheatService.Instance?.VehicleStats?.ResetSpeed(_shipSelected);
                    RefreshShipInfo();
                    CheatMenuStatus.Show(L.Wrf_StatusShipReset);
                },
                L.Wrf_ShipResetTip);
            var capRow = new Row((Px)CheatWidgets.Gap);
            capRow.SetChildren(x2, x3, x5, reset);

            // Hinweis: Ein Geschwindigkeits-Eingabefeld gab es hier zunaechst, aber Schiffe nutzen in ihrer
            // DrivingData eine ANDERE Speed-Einheit als LKW — der gemeinsame VehicleStats.SetSpeed-Pfad
            // skaliert dabei um Faktor 10 daneben (Eingabe 5 -> 0,5, verlangsamt statt beschleunigt). Bis die
            // Schiffs-Einheit sauber geklaert ist, zeigt die Info-Zeile die Geschwindigkeit nur an (kein Setzen).
            col.SetChildren(dropdown, _shipInfo, capRow);
            return col;
        }

        private void ApplyCapacity(int factor)
        {
            if (_shipSelected == null) return;
            CheatService.Instance?.Ships?.SetCapacityFactor(_shipSelected, factor);
            RefreshShipInfo();
            CheatMenuStatus.Show(L.Wrf_StatusShipCap(CheatWidgets.ProtoDisplayName(_shipSelected), factor));
        }

        // "Frachtschiff X — Kapazität: 300 % · Speed: 2,5" — Kapazitaet als Multiplikator-%, Speed in Tiles/Sek.
        private string ShipInfoText()
        {
            var ships = CheatService.Instance?.Ships;
            if (ships == null || _shipSelected == null) return string.Empty;
            string name = CheatWidgets.ProtoDisplayName(_shipSelected);
            int cap = ships.GetCapacityPercent(_shipSelected);
            double sp = CheatService.Instance?.VehicleStats?.GetSpeed(_shipSelected) ?? -1;
            string spStr = sp < 0 ? "—" : sp.ToString("0.##");
            return L.Wrf_ShipInfo(name, cap, spStr);
        }

        private void RefreshShipInfo()
        {
            if (_shipInfo is IComponentWithText t)
                t.SetValue(new LocStrFormatted(ShipInfoText()));
        }
    }
}
