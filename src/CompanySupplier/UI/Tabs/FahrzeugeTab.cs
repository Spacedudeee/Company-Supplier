using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Prototypes;
using Mafi.Core.Vehicles.Trucks;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Entities.Ships;
using Mafi.Core.Trains;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Fahrzeuge" (ui-spec V1–V3): Treibstoff, Fahrzeug-Limit, LKW-Kapazitaet.
    /// - V1 Toggle Treibstoff aus      -> FleetVehicle.SetFuelConsumptionDisabled(bool) [kein Status-Prop -> lokal]
    /// - V2 Stepper Fahrzeug-Limit ±5/±25/±50 (DELTA) -> FleetVehicle.ChangeVehicleLimit(int)
    /// - V3 Button-Gruppe LKW-Kapazitaet +100/+200/+500 % -> FleetVehicle.SetTruckCapacityMultiplier(int)
    ///      + "Zuruecksetzen" -> FleetVehicle.ResetTruckCapacity()
    ///      + Live-Liste aller LKW-Typen (Icon + Name + effektive Ladekapazitaet = Basis x Multiplikator).
    ///        Tabs sind statisch (kein RenderUpdate) -> nach jedem Kapazitaets-Button-Klick werden die
    ///        Zeilen-Labels event-getrieben neu berechnet (RefreshCapacityLabels).
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class FahrzeugeTab : ICheatTab
    {
        private readonly UiComponent _content;

        // V1 "Treibstoff aus" hat keine Backend-Status-Property -> lokal gehalten.
        private bool _fuelDisabled;

        // V2 "Fahrzeug-Limit": Label fuer das aktuelle Limit (wird nach Zahlenfeld/Stepper aktualisiert).
        private Label _limitLabel;

        // V3-Live-Kacheln: pro LKW-Typ das Kapazitaets-Label halten, damit es nach jedem Kapazitaets-Button-
        // Klick per SetValue neu gesetzt werden kann (Icon bleibt; Tabs haben keinen RenderUpdate-Hook).
        private readonly List<KeyValuePair<TruckProto, Label>> _capacityRows =
            new List<KeyValuePair<TruckProto, Label>>();

        // V3-Kopfzeile: zeigt den aktuell wirksamen Gesamt-Multiplikator (z. B. "Aktuell: 200 % (x2)").
        // Wird zusammen mit den Zeilen-Labels aktualisiert. SetValue auf Label ist nur ueber das
        // IComponentWithText-Interface erreichbar (explizite Impl.) -> Referenz als Label halten, beim
        // Refresh casten.
        private Label _multiplierSummary;

        // "Fahrzeug-Stats (pro Typ)": gewaehlter Fahrzeugtyp + Info-Label (Standard/Aktuell). Liste der
        // kapazitaetsfaehigen Typen (LKW/Bagger) fuer das Dropdown.
        private DrivingEntityProto _statsSelected;
        private Label _statsInfo;
        private IReadOnlyList<DrivingEntityProto> _statVehicles;

        // Kachel-Übersicht (alle Stats-Fahrzeuge mit aktueller Geschwindigkeit + Kapazität, live aktualisiert).
        private readonly List<StatTile> _statOverview = new List<StatTile>();
        private sealed class StatTile { public DrivingEntityProto Proto; public Label Cap; public Label Speed; }

        // "Zug-Waggon-Kapazität": gewaehlter Waggon-Typ + Info-Label + Liste (nur falls Zuege/DLC vorhanden).
        private CargoWagonProto _trainSelected;
        private Label _trainInfo;
        private IReadOnlyList<CargoWagonProto> _trainWagons;

        public FahrzeugeTab()
        {
            _content = BuildContent();

            // Heilung nach Save-Laden: der alte +10000%-Bug-Modifier (zeigte "20 → 2020") wird einmalig
            // beim Fenster-Aufbau entfernt; legitime Werte (+100/+200/+500 %) bleiben erhalten. Lief die
            // Bereinigung, startet die Liste frisch (20 → 20). BuildContent() lief schon, also stehen die
            // Zeilen-Referenzen in _capacityRows bereit fuer RefreshCapacityLabels().
            if (CheatService.Instance?.FleetVehicle?.SanitizeTruckCapacityIfAbsurd() == true)
                RefreshCapacityLabels();
        }

        public string Name => "Fahrzeuge";

        // "Vehicles"-Toolbar-Icon (Fahrzeuge). Verifizierter Const-Pfad aus Mafi.Unity.Assets
        // (Toolbar.Vehicles_svg). String-Pfad ist in 0.8.5.0 die robuste Variante (kein IconStyle
        // mehr); fehlt das Asset, rendert der Tab trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Vehicles.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Treibstoff"),
                BuildFuelToggle(),              // V1

                CheatWidgets.SectionTitle("Fahrzeug-Limit"),
                BuildVehicleLimitSection(),     // V2: aktuelles Limit + Zahlenfeld + Stepper

                CheatWidgets.SectionTitle("LKW-Kapazität"),
                BuildTruckCapacityButtons(),    // V3 (Buttons)
                BuildMultiplierSummary(),       // V3 (aktueller Gesamt-Multiplikator in %)
                BuildTruckCapacityList(),       // V3 (Live-Liste je LKW-Typ)

                CheatWidgets.SectionTitle("Fahrzeug-Stats (pro Typ)"),
                BuildVehicleStatsSection()      // exakte Kapazitaet pro Fahrzeugtyp (Reflection-Override)
            };

            // Zug-Waggon-Kapazitaet nur anhaengen, wenn (DLC-)Zuege vorhanden sind.
            AppendTrainSection(children);

            column.SetChildren(children.ToArray());
            return column;
        }

        // V1: Treibstoff-Verbrauch global an/aus. Aktiv = Verbrauch AUS.
        private UiComponent BuildFuelToggle()
        {
            return CheatWidgets.NewToggleRow(
                "Treibstoff-Verbrauch aus (aktiv = AUS)",
                _fuelDisabled,
                v =>
                {
                    _fuelDisabled = v;
                    CheatService.Instance?.FleetVehicle?.SetFuelConsumptionDisabled(v);
                },
                "Aktiv = Fahrzeuge verbrauchen keinen Treibstoff mehr.");
        }

        // V2: aktuelles Limit (Label) + absolutes Zahlenfeld + ±Stepper. Alle drei aktualisieren das Label.
        private UiComponent BuildVehicleLimitSection()
        {
            _limitLabel = new Label(new LocStrFormatted(LimitText()));

            var inputRow = CheatWidgets.NewIntInputRow(
                "Limit setzen",
                v =>
                {
                    CheatService.Instance?.FleetVehicle?.SetVehicleLimit(v);
                    RefreshLimit();
                    CheatMenuStatus.Show($"Fahrzeug-Limit = {v}");
                },
                min: 0);

            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();
            col.SetChildren(_limitLabel, inputRow, BuildVehicleLimitStepper());
            return col;
        }

        private static string LimitText()
        {
            int l = CheatService.Instance?.FleetVehicle?.GetVehicleLimit() ?? -1;
            return l < 0 ? "Aktuelles Limit: —" : $"Aktuelles Limit: {l}";
        }

        private void RefreshLimit()
        {
            if (_limitLabel is IComponentWithText t)
                t.SetValue(new LocStrFormatted(LimitText()));
        }

        // ±5/±25/±50-Stepper. ChangeVehicleLimit erwartet ein DELTA; nach jedem Klick das Limit-Label auffrischen.
        private UiComponent BuildVehicleLimitStepper()
        {
            var steps = new Dictionary<int, Action<int>>
            {
                { 5,  d => { CheatService.Instance?.FleetVehicle?.ChangeVehicleLimit(d); RefreshLimit(); } },
                { 25, d => { CheatService.Instance?.FleetVehicle?.ChangeVehicleLimit(d); RefreshLimit(); } },
                { 50, d => { CheatService.Instance?.FleetVehicle?.ChangeVehicleLimit(d); RefreshLimit(); } },
            };
            return CheatWidgets.NewIncrementButtonGroup(steps);
        }

        // V3: LKW-Kapazitaets-Multiplikator als absolute Prozent-Buttons + Zuruecksetzen.
        // SetTruckCapacityMultiplier(percent) setzt den Multiplikator absolut (z. B. 500 = 5x Fracht/LKW).
        // Nach jedem Klick die Live-Liste neu berechnen (event-getrieben, kein RenderUpdate-Hook).
        private UiComponent BuildTruckCapacityButtons()
        {
            var plus100 = new ButtonText(
                Button.Primary, new LocStrFormatted("+100 %"),
                () => ApplyTruckCapacityMultiplier(100));
            var plus200 = new ButtonText(
                Button.Primary, new LocStrFormatted("+200 %"),
                () => ApplyTruckCapacityMultiplier(200));
            var plus500 = new ButtonText(
                Button.Primary, new LocStrFormatted("+500 %"),
                () => ApplyTruckCapacityMultiplier(500));

            var reset = CheatWidgets.DangerButton(
                "Zurücksetzen",
                () =>
                {
                    CheatService.Instance?.FleetVehicle?.ResetTruckCapacity();
                    RefreshCapacityLabels();
                },
                "Entfernt den LKW-Kapazitäts-Multiplikator (zurück auf Normal).");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(plus100, plus200, plus500, reset);
            return row;
        }

        /// <summary>Setzt den Multiplikator und aktualisiert danach die Live-Liste.</summary>
        private void ApplyTruckCapacityMultiplier(int percent)
        {
            CheatService.Instance?.FleetVehicle?.SetTruckCapacityMultiplier(percent);
            RefreshCapacityLabels();
        }

        // V3: Kopfzeile mit dem aktuell wirksamen Gesamt-Multiplikator (Klarheit). Reset-Zustand = 100 % (x1).
        private UiComponent BuildMultiplierSummary()
        {
            _multiplierSummary = new Label(new LocStrFormatted(MultiplierSummaryText()));
            return _multiplierSummary;
        }

        /// <summary>"Aktueller Multiplikator: 200 % (x2)" — der per <c>GetTruckCapacityMultiplier</c>
        /// gelieferte Gesamtwert (Basis + Modifier), exakt wie das Spiel ihn auf die Kapazitaet anwendet.</summary>
        private static string MultiplierSummaryText()
        {
            Percent mult = CheatService.Instance?.FleetVehicle?.GetTruckCapacityMultiplier() ?? Percent.Hundred;
            int percentVal = mult.ToIntPercentRounded();
            return $"Aktueller Multiplikator: {percentVal} %  (x{percentVal / 100.0:0.##})";
        }

        // V3: LKW-Kacheln (grosses Icon + Kapazitaet, OHNE Namen — am Icon erkennbar) in einer Wrap-Reihe,
        // die in die Breite fliesst (mehrere nebeneinander) statt untereinander. H-Varianten (Id endet auf
        // "H") werden ausgeblendet, da sie dieselbe Kapazitaet wie die Basis-Variante haben (Duplikate).
        private UiComponent BuildTruckCapacityList()
        {
            _capacityRows.Clear();

            var protos = CheatService.Instance?.Protos;
            if (protos == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] FahrzeugeTab: ProtosDb nicht verfuegbar — LKW-Liste bleibt leer.");
                var fallback = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();
                fallback.SetChildren(new Label(new LocStrFormatted("(LKW-Typen nicht verfügbar)")));
                return fallback;
            }

            var trucks = protos.Filter<TruckProto>(t => !t.Id.ToString().EndsWith("H", StringComparison.Ordinal))
                               .OrderBy(t => t.CapacityBase.Value)
                               .ToList();

            // Wrap-Reihe: Kacheln fliessen horizontal und brechen um, statt eine lange vertikale Liste zu bilden.
            var wrap = new Row((Px)18).Wrap(true);
            var tiles = new List<UiComponent>(trucks.Count);
            foreach (TruckProto truck in trucks)
            {
                // Grosses Icon (Tooltip zeigt den LKW-Namen) ueber dem Kapazitaets-Label, zentriert.
                var icon = new ButtonIcon(Button.None, (IProtoWithIcon)truck, () => { }).IconSize((Px)52);
                var capLabel = new Label(new LocStrFormatted(CapacityText(truck)));
                var tile = new Column((Px)4).AlignItemsCenter();
                tile.SetChildren(icon, capLabel);
                _capacityRows.Add(new KeyValuePair<TruckProto, Label>(truck, capLabel));
                tiles.Add(tile);
            }

            if (tiles.Count == 0)
                tiles.Add(new Label(new LocStrFormatted("(keine LKW-Typen gefunden)")));

            wrap.SetChildren(tiles.ToArray());
            return wrap;
        }

        /// <summary>Berechnet alle Zeilen-Labels + die Multiplikator-Kopfzeile neu (nach Multiplikator-
        /// Aenderung). Icon bleibt erhalten.</summary>
        private void RefreshCapacityLabels()
        {
            foreach (var entry in _capacityRows)
                if (entry.Value is IComponentWithText t)
                    t.SetValue(new LocStrFormatted(CapacityText(entry.Key)));

            // SetValue auf Label ist eine explizite IComponentWithText-Implementierung -> ueber das
            // Interface aufrufen.
            if (_multiplierSummary is IComponentWithText summary)
                summary.SetValue(new LocStrFormatted(MultiplierSummaryText()));
        }

        /// <summary>"180 → 360" (Basis → effektiv) bzw. nur "180" im Reset-Zustand — OHNE Namen, der LKW-Typ
        /// ist am Icon erkennbar. Effektiv = CapacityBase.ScaledBy(aktueller Multiplikator), wie im Spiel
        /// (Truck.onCapacityMultiplierChange). Reset-Zustand: mult = 100% -> Basis unveraendert.</summary>
        private static string CapacityText(TruckProto truck)
        {
            int baseCap = truck.CapacityBase.Value;
            Percent mult = CheatService.Instance?.FleetVehicle?.GetTruckCapacityMultiplier() ?? Percent.Hundred;
            int effective = truck.CapacityBase.ScaledBy(mult).Value;
            return baseCap == effective ? $"{baseCap}" : $"{baseCap} → {effective}";
        }

        // ------------------------------------------------------------------------------------------
        // Fahrzeug-Stats pro Typ (exakte Werte, Reflection-Override via VehicleStatsCheats)
        // ------------------------------------------------------------------------------------------

        /// <summary>Dropdown (kapazitaetsfaehige Fahrzeugtypen) + Info-Label + exaktes Kapazitaets-Eingabefeld
        /// + "Zuruecksetzen". Anders als die globalen %-Buttons oben wirkt das EXAKT und PRO TYP.</summary>
        private UiComponent BuildVehicleStatsSection()
        {
            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();

            var protos = CheatService.Instance?.Protos;
            var stats = CheatService.Instance?.VehicleStats;
            if (protos == null || stats == null)
            {
                col.SetChildren(new Label(new LocStrFormatted("(Fahrzeug-Stats nicht verfügbar)")));
                return col;
            }

            // Kapazitaetsfaehige Typen (LKW/Bagger); H-Varianten ausblenden (Duplikate, wie oben).
            // Boden-Fahrzeuge mit Icon (LKW/Bagger/Holzvollernter/Baumpflanzer/Raketen). Schiffe (ShipProto:
            // Welt-Schiff + Frachtschiffe) sind ebenfalls DrivingEntityProto -> raus. H-Varianten (Duplikate) raus.
            _statVehicles = protos.Filter<DrivingEntityProto>(p => p is IProtoWithIcon
                                    && !(p is ShipProto)
                                    && !p.Id.ToString().EndsWith("H", StringComparison.Ordinal))
                                  .OrderBy(p => CheatWidgets.ProtoDisplayName(p))
                                  .ToList();
            _statsSelected = _statVehicles.Count > 0 ? _statVehicles[0] : null;

            Dropdown<DrivingEntityProto>.OptionFactory factory =
                (DrivingEntityProto proto, int index, bool isInDropdown) =>
                    new ButtonIconText(Button.None, (IProtoWithIcon)proto, CheatWidgets.ProtoDisplayLabel(proto));

            var dropdown = new Dropdown<DrivingEntityProto>(factory, null, null, false);
            dropdown.Label(new LocStrFormatted("Fahrzeug"));
            dropdown.SetOptions(_statVehicles);
            dropdown.OnValueChanged((DrivingEntityProto proto, int idx) => { _statsSelected = proto; RefreshStatsInfo(); });
            dropdown.FlexGrow(1f);
            if (_statVehicles.Count > 0) dropdown.SetValueIndex(0, notifyChangeListeners: false);

            _statsInfo = new Label(new LocStrFormatted(StatsInfoText()));

            var speedRow = CheatWidgets.NewFloatInputRow(
                "Geschwindigkeit", v =>
                {
                    CheatService.Instance?.VehicleStats?.SetSpeed(_statsSelected, v);
                    RefreshStatsInfo();
                    if (_statsSelected != null)
                        CheatMenuStatus.Show($"Geschwindigkeit {CheatWidgets.ProtoDisplayName(_statsSelected)} = {v:0.##}");
                },
                min: 0.1f);

            var capacityRow = CheatWidgets.NewIntInputRow(
                "Ladekapazität", v =>
                {
                    CheatService.Instance?.VehicleStats?.SetCapacity(_statsSelected, v);
                    RefreshStatsInfo();
                    if (_statsSelected != null)
                        CheatMenuStatus.Show($"Kapazität {CheatWidgets.ProtoDisplayName(_statsSelected)} = {v}");
                },
                min: 1);

            var reset = CheatWidgets.DangerButton(
                "Zurücksetzen",
                () =>
                {
                    CheatService.Instance?.VehicleStats?.ResetSpeed(_statsSelected);
                    CheatService.Instance?.VehicleStats?.ResetCapacity(_statsSelected);
                    RefreshStatsInfo();
                    CheatMenuStatus.Show("Fahrzeug-Stats zurückgesetzt");
                },
                "Setzt Geschwindigkeit + Kapazität des gewählten Fahrzeugtyps auf den Standard zurück.");

            col.SetChildren(dropdown, _statsInfo, speedRow, capacityRow, reset,
                CheatWidgets.SectionTitle("Übersicht"), BuildStatsOverview());
            return col;
        }

        /// <summary>"LKW — Kapazität: 360 (Standard: 180)" — Default = gesnapshotteter Originalwert.</summary>
        private string StatsInfoText()
        {
            var stats = CheatService.Instance?.VehicleStats;
            if (stats == null || _statsSelected == null) return string.Empty;
            string name = CheatWidgets.ProtoDisplayName(_statsSelected);

            double sp = stats.GetSpeed(_statsSelected);
            double spDef = stats.GetDefaultSpeed(_statsSelected);
            string speedPart = Math.Abs(sp - spDef) < 0.005
                ? $"Speed: {sp:0.##}"
                : $"Speed: {sp:0.##} (Standard: {spDef:0.##})";

            string capPart;
            if (stats.HasCapacity(_statsSelected))
            {
                int cap = stats.GetCapacity(_statsSelected);
                int capDef = stats.GetDefaultCapacity(_statsSelected);
                capPart = cap == capDef ? $"Kapazität: {cap}" : $"Kapazität: {cap} (Standard: {capDef})";
            }
            else capPart = "Kapazität: —";

            return $"{name} — {speedPart} | {capPart}";
        }

        private void RefreshStatsInfo()
        {
            if (_statsInfo is IComponentWithText t)
                t.SetValue(new LocStrFormatted(StatsInfoText()));
            RefreshStatsOverview();
        }

        /// <summary>Kachel-Übersicht (Icon + Geschwindigkeit + Kapazität) ALLER Stats-Fahrzeuge — alles auf einen
        /// Blick, analog zur LKW-Kapazitätsliste. Die Labels werden in <see cref="_statOverview"/> gehalten und
        /// nach jeder Änderung (Set/Reset) per <see cref="RefreshStatsOverview"/> neu berechnet.</summary>
        private UiComponent BuildStatsOverview()
        {
            _statOverview.Clear();
            var wrap = new Row((Px)18).Wrap(true);
            var tiles = new List<UiComponent>(_statVehicles.Count);
            foreach (var p in _statVehicles)
            {
                var icon = new ButtonIcon(Button.None, (IProtoWithIcon)p, () => { }).IconSize((Px)40);
                var spd = new Label(new LocStrFormatted(OverviewSpeedText(p)));
                var cap = new Label(new LocStrFormatted(OverviewCapText(p)));
                var tile = new Column((Px)2).AlignItemsCenter();
                tile.SetChildren(icon, spd, cap);
                _statOverview.Add(new StatTile { Proto = p, Cap = cap, Speed = spd });
                tiles.Add(tile);
            }
            wrap.SetChildren(tiles.ToArray());
            return wrap;
        }

        // "Kap 360" bzw. "Kap —" (Typen ohne Ladekapazität, z. B. Raketen).
        private static string OverviewCapText(DrivingEntityProto p)
        {
            var stats = CheatService.Instance?.VehicleStats;
            return (stats != null && stats.HasCapacity(p)) ? $"Kap {stats.GetCapacity(p)}" : "Kap —";
        }

        // "v 2,5" (Tiles/Sek, eine Nachkommastelle).
        private static string OverviewSpeedText(DrivingEntityProto p)
        {
            double s = CheatService.Instance?.VehicleStats?.GetSpeed(p) ?? -1;
            return s < 0 ? "v —" : $"v {s:0.#}";
        }

        private void RefreshStatsOverview()
        {
            foreach (var r in _statOverview)
            {
                if (r.Cap is IComponentWithText ct) ct.SetValue(new LocStrFormatted(OverviewCapText(r.Proto)));
                if (r.Speed is IComponentWithText st) st.SetValue(new LocStrFormatted(OverviewSpeedText(r.Proto)));
            }
        }

        // ------------------------------------------------------------------------------------------
        // Zug-Waggon-Kapazitaet pro Waggon-Typ (exakter Wert, via TrainCheats) — nur falls Zuege vorhanden
        // ------------------------------------------------------------------------------------------

        /// <summary>Haengt den Zug-Abschnitt nur an, wenn Cargo-Waggons existieren (Basis-Spiel ODER Zug-DLC).
        /// Ohne Zuege bleibt der Reiter unveraendert.</summary>
        private void AppendTrainSection(List<UiComponent> children)
        {
            var protos = CheatService.Instance?.Protos;
            if (protos == null) return;

            _trainWagons = protos.Filter<CargoWagonProto>(w => w is IProtoWithIcon)
                                 .OrderBy(w => CheatWidgets.ProtoDisplayName(w))
                                 .ToList();
            if (_trainWagons.Count == 0) return; // keine Zuege/DLC -> Abschnitt komplett weglassen

            _trainSelected = _trainWagons[0];
            children.Add(CheatWidgets.SectionTitle("Zug-Waggon-Kapazität"));
            children.Add(BuildTrainStatsSection());
        }

        private UiComponent BuildTrainStatsSection()
        {
            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();

            Dropdown<CargoWagonProto>.OptionFactory factory =
                (CargoWagonProto wagon, int index, bool isInDropdown) =>
                    new ButtonIconText(Button.None, (IProtoWithIcon)wagon, CheatWidgets.ProtoDisplayLabel(wagon));

            var dropdown = new Dropdown<CargoWagonProto>(factory, null, null, false);
            dropdown.Label(new LocStrFormatted("Waggon"));
            dropdown.SetOptions(_trainWagons);
            dropdown.OnValueChanged((CargoWagonProto w, int idx) => { _trainSelected = w; RefreshTrainInfo(); });
            dropdown.FlexGrow(1f);
            if (_trainWagons.Count > 0) dropdown.SetValueIndex(0, notifyChangeListeners: false);

            _trainInfo = new Label(new LocStrFormatted(TrainInfoText()));

            var capacityRow = CheatWidgets.NewIntInputRow(
                "Kapazität", v =>
                {
                    CheatService.Instance?.Train?.SetCapacity(_trainSelected, v);
                    RefreshTrainInfo();
                    if (_trainSelected != null)
                        CheatMenuStatus.Show($"Waggon-Kapazität {CheatWidgets.ProtoDisplayName(_trainSelected)} = {v}");
                },
                min: 1);

            var reset = CheatWidgets.DangerButton(
                "Zurücksetzen",
                () =>
                {
                    CheatService.Instance?.Train?.ResetCapacity(_trainSelected);
                    RefreshTrainInfo();
                    CheatMenuStatus.Show("Waggon-Kapazität zurückgesetzt");
                },
                "Setzt die Kapazität des gewählten Waggon-Typs auf den Standard zurück.");

            col.SetChildren(dropdown, _trainInfo, capacityRow, reset);
            return col;
        }

        private string TrainInfoText()
        {
            var train = CheatService.Instance?.Train;
            if (train == null || _trainSelected == null) return string.Empty;
            int cur = train.GetCapacity(_trainSelected);
            int def = train.GetDefaultCapacity(_trainSelected);
            string name = CheatWidgets.ProtoDisplayName(_trainSelected);
            return cur == def ? $"{name} — Kapazität: {cur}" : $"{name} — Kapazität: {cur} (Standard: {def})";
        }

        private void RefreshTrainInfo()
        {
            if (_trainInfo is IComponentWithText t)
                t.SetValue(new LocStrFormatted(TrainInfoText()));
        }
    }
}
