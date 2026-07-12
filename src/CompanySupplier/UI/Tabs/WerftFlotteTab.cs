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
        private Label _shipyardInfo;
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
                RefreshShipyardInfo();
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
                BuildCargoShipSection(),

                CheatWidgets.SectionTitle(L.Wrf_TitleShipyard),
                BuildShipyardSection()
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

        // Frachtschiff-Kapazitaet (×2/×3/×5) + Geschwindigkeit — GLOBAL fuer ALLE Frachtschiff-Typen.
        // Die vier Typen sind in-game optisch nicht unterscheidbar, daher KEIN Dropdown: jede Aenderung wirkt
        // auf alle zugleich. Ohne Frachtschiffe -> Hinweis.
        private UiComponent BuildCargoShipSection()
        {
            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();

            var ships = CheatService.Instance?.Ships;
            _cargoShips = ships?.GetCargoShips().ToList()
                          ?? (IReadOnlyList<CargoShipProto>)Array.Empty<CargoShipProto>();

            if (ships == null || _cargoShips.Count == 0)
            {
                col.SetChildren(new Label(new LocStrFormatted(L.Wrf_NoShips)));
                return col;
            }

            // Repraesentant fuer die Anzeige (alle werden einheitlich gesetzt).
            _shipSelected = _cargoShips[0];
            _shipInfo = new Label(new LocStrFormatted(ShipInfoText()));

            // Kapazitaet: ×2/×3/×5 (gruen) + Zuruecksetzen (rot) — auf ALLE Frachtschiffe.
            var x2 = new ButtonText(Button.Primary, new LocStrFormatted("×2"), () => ApplyCapacity(2));
            var x3 = new ButtonText(Button.Primary, new LocStrFormatted("×3"), () => ApplyCapacity(3));
            var x5 = new ButtonText(Button.Primary, new LocStrFormatted("×5"), () => ApplyCapacity(5));
            var reset = CheatWidgets.DangerButton(
                L.Common_Reset,
                () =>
                {
                    CheatService.Instance?.Ships?.ResetAllCapacity();
                    foreach (var s in _cargoShips) CheatService.Instance?.VehicleStats?.ResetSpeed(s);
                    RefreshShipInfo();
                    CheatMenuStatus.Show(L.Wrf_StatusShipReset);
                },
                L.Wrf_ShipResetTip);
            var capRow = new Row((Px)CheatWidgets.Gap);
            capRow.SetChildren(x2, x3, x5, reset);

            // Geschwindigkeit exakt setzen — auf ALLE Frachtschiffe (Frachtschiffe sind DrivingEntityProto ->
            // gemeinsamer VehicleStats-Pfad mit konsistenter roher Skala; siehe VehicleStatsCheats.WriteSpeedField).
            var speedRow = CheatWidgets.NewFloatInputRow(
                L.Fzg_Speed,
                v =>
                {
                    foreach (var s in _cargoShips) CheatService.Instance?.VehicleStats?.SetSpeed(s, v);
                    RefreshShipInfo();
                    CheatMenuStatus.Show(L.Wrf_StatusShipSpeed(v.ToString("0.##")));
                },
                min: 0.1f);

            col.SetChildren(_shipInfo, capRow, speedRow);
            return col;
        }

        private void ApplyCapacity(int factor)
        {
            CheatService.Instance?.Ships?.SetAllCapacityFactor(factor);
            RefreshShipInfo();
            CheatMenuStatus.Show(L.Wrf_StatusCargoCap(factor));
        }

        // "Frachtschiffe — Kapazität: 300 % · Speed: 2,5" — repraesentativ (alle einheitlich gesetzt).
        private string ShipInfoText()
        {
            var ships = CheatService.Instance?.Ships;
            if (ships == null || _shipSelected == null) return string.Empty;
            int cap = ships.GetCapacityPercent(_shipSelected);
            double sp = CheatService.Instance?.VehicleStats?.GetSpeed(_shipSelected) ?? -1;
            string spStr = sp < 0 ? "—" : sp.ToString("0.##");
            return L.Wrf_CargoInfo(cap, spStr);
        }

        private void RefreshShipInfo()
        {
            if (_shipInfo is IComponentWithText t)
                t.SetValue(new LocStrFormatted(ShipInfoText()));
        }

        // Werft-Lager: Kapazitaet aller Werft-Stufen (×2/×3/×5, gruen + Reset rot) plus Fracht-Aktionen
        // (Werft-Fracht zerstoeren / ins Basis-Lager umlegen).
        private UiComponent BuildShipyardSection()
        {
            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();

            _shipyardInfo = new Label(new LocStrFormatted(ShipyardCapText()));

            var x2 = new ButtonText(Button.Primary, new LocStrFormatted("×2"), () => ApplyShipyardCapacity(2));
            var x3 = new ButtonText(Button.Primary, new LocStrFormatted("×3"), () => ApplyShipyardCapacity(3));
            var x5 = new ButtonText(Button.Primary, new LocStrFormatted("×5"), () => ApplyShipyardCapacity(5));
            var reset = CheatWidgets.DangerButton(
                L.Common_Reset,
                () =>
                {
                    CheatService.Instance?.Ships?.ResetShipyardCapacity();
                    RefreshShipyardInfo();
                    CheatMenuStatus.Show(L.Wrf_StatusShipyardReset);
                },
                L.Wrf_ShipyardCapTip);
            var capRow = new Row((Px)CheatWidgets.Gap);
            capRow.SetChildren(x2, x3, x5, reset);

            // Werft-Fracht sofort verwerfen (rot) oder ins globale Basis-Lager umlegen (gruen).
            var destroy = CheatWidgets.DangerButton(
                L.Wrf_DestroyCargo,
                () =>
                {
                    CheatService.Instance?.FleetVehicle?.DestroyShipyardCargo();
                    CheatMenuStatus.Show(L.Wrf_StatusCargoDestroyed);
                },
                L.Wrf_DestroyCargoTip);
            var dump = CheatWidgets.PrimaryButton(
                L.Wrf_DumpCargo,
                () =>
                {
                    CheatService.Instance?.FleetVehicle?.DumpShipyardCargoToBase();
                    CheatMenuStatus.Show(L.Wrf_StatusCargoDumped);
                },
                L.Wrf_DumpCargoTip);
            var cargoRow = new Row((Px)CheatWidgets.Gap);
            cargoRow.SetChildren(destroy, dump);

            col.SetChildren(_shipyardInfo, capRow, cargoRow);
            return col;
        }

        private void ApplyShipyardCapacity(int factor)
        {
            CheatService.Instance?.Ships?.SetShipyardCapacityFactor(factor);
            RefreshShipyardInfo();
            CheatMenuStatus.Show(L.Wrf_StatusShipyardCap(factor));
        }

        // "Lager-Kapazität: 900" — aktuelle Kapazitaet der ersten Werft; "—" wenn keine vorhanden (<0).
        private string ShipyardCapText()
        {
            int cap = CheatService.Instance?.Ships?.GetFirstShipyardCapacity() ?? -1;
            return L.Wrf_ShipyardCapInfo(cap < 0 ? "—" : cap.ToString());
        }

        private void RefreshShipyardInfo()
        {
            if (_shipyardInfo is IComponentWithText t)
                t.SetValue(new LocStrFormatted(ShipyardCapText()));
        }
    }
}
