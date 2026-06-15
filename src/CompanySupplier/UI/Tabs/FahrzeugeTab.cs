using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Prototypes;
using Mafi.Core.Vehicles.Trucks;
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

        // V3-Live-Kacheln: pro LKW-Typ das Kapazitaets-Label halten, damit es nach jedem Kapazitaets-Button-
        // Klick per SetValue neu gesetzt werden kann (Icon bleibt; Tabs haben keinen RenderUpdate-Hook).
        private readonly List<KeyValuePair<TruckProto, Label>> _capacityRows =
            new List<KeyValuePair<TruckProto, Label>>();

        // V3-Kopfzeile: zeigt den aktuell wirksamen Gesamt-Multiplikator (z. B. "Aktuell: 200 % (x2)").
        // Wird zusammen mit den Zeilen-Labels aktualisiert. SetValue auf Label ist nur ueber das
        // IComponentWithText-Interface erreichbar (explizite Impl.) -> Referenz als Label halten, beim
        // Refresh casten.
        private Label _multiplierSummary;

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
                BuildVehicleLimitStepper(),     // V2

                CheatWidgets.SectionTitle("LKW-Kapazität"),
                BuildTruckCapacityButtons(),    // V3 (Buttons)
                BuildMultiplierSummary(),       // V3 (aktueller Gesamt-Multiplikator in %)
                BuildTruckCapacityList()        // V3 (Live-Liste je LKW-Typ)
            };

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

        // V2: Fahrzeug-Limit-Stepper ±5/±25/±50. ChangeVehicleLimit erwartet ein DELTA
        // (positiv erhoeht, negativ verringert) -> direkt auf die Inkrement-Gruppe abbildbar.
        private UiComponent BuildVehicleLimitStepper()
        {
            var steps = new Dictionary<int, Action<int>>
            {
                { 5,  d => CheatService.Instance?.FleetVehicle?.ChangeVehicleLimit(d) },
                { 25, d => CheatService.Instance?.FleetVehicle?.ChangeVehicleLimit(d) },
                { 50, d => CheatService.Instance?.FleetVehicle?.ChangeVehicleLimit(d) },
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
    }
}
