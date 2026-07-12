using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core;                                   // IdsCore.PropertyIds
using Mafi.Core.Prototypes;                        // ProtosDb, IProtoWithIcon
using Mafi.Core.PropertiesDb;                      // IPropertiesDb, IProperty<T>, PropertyModifiers, PropertyModifier<T>
using Mafi.Core.Buildings.Cargo.Ships;             // CargoShipProto
using Mafi.Core.Buildings.Shipyard;                // ShipyardProto (Werft-Lager-Kapazitaet)

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Schiffe": globaler Treibstoff-Hebel + Frachtschiff-Kapazitaet pro Typ.
    ///
    /// - Treibstoff: globale Percent-Property <c>ShipsFuelConsumptionMultiplier</c> auf -100% (= 0 Verbrauch)
    ///   ueber einen eigenen PropertyModifier (Owner <see cref="ModifierOwner"/>) — exakt das Muster von
    ///   <c>FleetVehicleCheats.SetTruckCapacityMultiplier</c> / <c>WorldMapCheats.SetTradeBoost</c>. Save-sicher,
    ///   registry-faehig (Panik-Aus / Presets / Auto-Restore).
    /// - Frachtschiff-Kapazitaet: es gibt KEINE globale Property dafuer — die Kapazitaet skaliert je Typ ueber
    ///   das (readonly) Proto-Feld <c>CargoShipProto.CapacityMultiplier</c> (Percent). Wir ueberschreiben es per
    ///   Reflection (wie <see cref="VehicleStatsCheats"/> die LKW-Kapazitaet) und snapshotten den Originalwert
    ///   LAZY beim ersten Edit fuer den Reset. Protos werden je Sitzung neu gebaut (nicht im Save), daher ist der
    ///   Snapshot session-lokal ausreichend.
    ///
    /// Schiffs-GESCHWINDIGKEIT braucht KEINEN eigenen Code: <c>CargoShipProto</c> ist ein
    /// <c>DrivingEntityProto</c> -> <see cref="VehicleStatsCheats.SetSpeed"/> greift direkt.
    ///
    /// Robustheit (Early-Access-Interna koennen driften): jeder Zugriff in try/catch + Log.Warning.
    /// </summary>
    public sealed class ShipCheats
    {
        private const string ModifierOwner = "CompanySupplier.Ships";
        private const BindingFlags InstAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly DependencyResolver _resolver;
        private IPropertiesDb _propertiesDb;
        private ProtosDb _protos;

        private bool _fuelDisabledCached;

        // Original-CapacityMultiplier je CargoShipProto-Id (gesnapshottet VOR dem ersten Override) -> Reset.
        private readonly Dictionary<string, Percent> _origCapMult = new Dictionary<string, Percent>();

        public ShipCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _propertiesDb);
            _resolver.TryResolve<ProtosDb>(out _protos);
        }

        // ------------------------------------------------------------------------------------------
        // Treibstoff (global) — ShipsFuelConsumptionMultiplier @ -100%
        // ------------------------------------------------------------------------------------------

        /// <summary>Schiffs-Treibstoff aus? Live aus der Praesenz UNSERES Modifiers gelesen (ueberlebt im Save);
        /// Fallback: zuletzt gesetzter Wert, wenn die PropertiesDb nicht lesbar ist.</summary>
        public bool ShipsFuelDisabled
        {
            get
            {
                try
                {
                    IProperty<Percent> prop = _propertiesDb?.GetProperty(IdsCore.PropertyIds.ShipsFuelConsumptionMultiplier);
                    if (prop != null) return prop.TryGetModifier(ModifierOwner, out PropertyModifier<Percent> _);
                }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ShipsFuelDisabled lesen: {ex.Message}"); }
                return _fuelDisabledCached;
            }
        }

        /// <summary>Schaltet den Treibstoff-Verbrauch aller Schiffe global an/aus (-100% Verbrauch).</summary>
        public void SetShipsFuelDisabled(bool disabled)
        {
            if (_propertiesDb == null) return;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(IdsCore.PropertyIds.ShipsFuelConsumptionMultiplier);
                if (prop == null) return;

                if (disabled)
                    prop.AddOrSetModifier(ModifierOwner, Percent.FromPercentVal(-100), PropertyModifiers.NO_GROUP);
                else
                    prop.TryRemoveModifier(ModifierOwner);

                _fuelDisabledCached = disabled;
                Log.Info($"[{CompanySupplier.ModName}] Schiffs-Treibstoff deaktiviert = {disabled}.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetShipsFuelDisabled: {ex.Message}"); }
        }

        // ------------------------------------------------------------------------------------------
        // Frachtschiff-Kapazitaet pro Typ — CargoShipProto.CapacityMultiplier (Reflection)
        // ------------------------------------------------------------------------------------------

        /// <summary>Alle Frachtschiff-Typen mit Icon (fuers Dropdown), oder leer wenn die ProtosDb fehlt.</summary>
        public IReadOnlyList<CargoShipProto> GetCargoShips()
        {
            if (_protos == null) return Array.Empty<CargoShipProto>();
            try { return _protos.Filter<CargoShipProto>(p => p is IProtoWithIcon).ToList(); }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] GetCargoShips: {ex.Message}");
                return Array.Empty<CargoShipProto>();
            }
        }

        /// <summary>Aktueller Kapazitaets-Multiplikator des Typs in Prozent (100 = Normal).</summary>
        public int GetCapacityPercent(CargoShipProto proto)
        {
            try { return proto == null ? 100 : proto.CapacityMultiplier.ToIntPercentRounded(); }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] GetCapacityPercent({proto?.Id}): {ex.Message}"); return 100; }
        }

        /// <summary>Original-(Default-)Multiplikator: der gesnapshottete Wert, sonst der aktuelle (= unveraendert).</summary>
        public int GetDefaultCapacityPercent(CargoShipProto proto)
        {
            if (proto != null && _origCapMult.TryGetValue(proto.Id.ToString(), out Percent o)) return o.ToIntPercentRounded();
            return GetCapacityPercent(proto);
        }

        /// <summary>True, wenn dieser Typ aktuell einen aktiven Kapazitaets-Override hat (fuer die UI).</summary>
        public bool HasCapacityOverride(CargoShipProto proto)
            => proto != null && _origCapMult.ContainsKey(proto.Id.ToString());

        /// <summary>Skaliert die Kapazitaet des Typs auf das <paramref name="factor"/>-fache des Originals
        /// (z. B. 2 = doppelte Fracht). Snapshottet vorher den Originalwert fuer den Reset.</summary>
        public void SetCapacityFactor(CargoShipProto proto, int factor)
        {
            if (proto == null || factor < 1) return;
            try
            {
                Percent current = proto.CapacityMultiplier;
                SnapshotCapacity(proto, current);
                Percent orig = _origCapMult[proto.Id.ToString()];
                int targetPercent = Math.Max(1, orig.ToIntPercentRounded() * factor);
                WriteCapacityField(proto, Percent.FromPercentVal(targetPercent));
                Log.Info($"[{CompanySupplier.ModName}] Frachtschiff-Kapazitaet {proto.Id} = x{factor} ({targetPercent}%) gesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetCapacityFactor({proto.Id}): {ex.Message}"); }
        }

        /// <summary>Setzt die Kapazitaet des Typs auf den gesnapshotteten Originalwert zurueck (no-op, wenn nie veraendert).</summary>
        public void ResetCapacity(CargoShipProto proto)
        {
            if (proto == null || !_origCapMult.TryGetValue(proto.Id.ToString(), out Percent orig)) return;
            try
            {
                WriteCapacityField(proto, orig);
                _origCapMult.Remove(proto.Id.ToString());
                Log.Info($"[{CompanySupplier.ModName}] Frachtschiff-Kapazitaet {proto.Id} zurueckgesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ResetCapacity({proto.Id}): {ex.Message}"); }
        }

        private void SnapshotCapacity(CargoShipProto proto, Percent currentValue)
        {
            string id = proto.Id.ToString();
            if (!_origCapMult.ContainsKey(id)) _origCapMult[id] = currentValue;
        }

        // ------------------------------------------------------------------------------------------
        // Werft-Lager-Kapazitaet — ShipyardProto.CargoCapacity (Reflection, alle Tiers auf einmal)
        // ------------------------------------------------------------------------------------------
        //
        // Anders als Frachtschiffe hat die Werft KEINEN Multiplikator, sondern ein absolutes Lager-Feld
        // CargoCapacity (Quantity). Die Werft ist eine Gebaeude-Familie mit mehreren Tiers — wir skalieren
        // ALLE Tiers zugleich um denselben Faktor (relativ zum jeweiligen Original) und snapshotten je Tier
        // fuer den Reset. Die laufende Werft liest m_proto.CargoCapacity live -> wirkt sofort auch auf
        // bestehende Werften. Original-Kapazitaet je ShipyardProto-Id:
        private readonly Dictionary<string, int> _origShipyardCap = new Dictionary<string, int>();

        /// <summary>Alle Werft-Typen (Tiers), oder leer wenn die ProtosDb fehlt.</summary>
        public IReadOnlyList<ShipyardProto> GetShipyards()
        {
            if (_protos == null) return Array.Empty<ShipyardProto>();
            try { return _protos.Filter<ShipyardProto>(p => true).ToList(); }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] GetShipyards: {ex.Message}");
                return Array.Empty<ShipyardProto>();
            }
        }

        /// <summary>True, wenn irgendein Werft-Tier aktuell einen Kapazitaets-Override hat (fuer die UI).</summary>
        public bool HasShipyardCapacityOverride => _origShipyardCap.Count > 0;

        /// <summary>Repraesentative aktuelle Lager-Kapazitaet (erster Werft-Tier) fuer die UI-Anzeige, oder -1.</summary>
        public int GetFirstShipyardCapacity()
        {
            var list = GetShipyards();
            if (list.Count == 0) return -1;
            try { return list[0].CargoCapacity.Value; }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] GetFirstShipyardCapacity: {ex.Message}"); return -1; }
        }

        /// <summary>Skaliert die Lager-Kapazitaet ALLER Werft-Tiers auf das <paramref name="factor"/>-fache des
        /// jeweiligen Originals. Snapshottet je Tier den Originalwert (capture-on-first-edit) fuer den Reset.</summary>
        public void SetShipyardCapacityFactor(int factor)
        {
            if (factor < 1) return;
            int count = 0;
            foreach (var proto in GetShipyards())
            {
                try
                {
                    string id = proto.Id.ToString();
                    if (!_origShipyardCap.ContainsKey(id)) _origShipyardCap[id] = proto.CargoCapacity.Value;
                    int target = Math.Max(1, _origShipyardCap[id] * factor);
                    WriteShipyardCapacity(proto, target);
                    count++;
                }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetShipyardCapacityFactor({proto?.Id}): {ex.Message}"); }
            }
            Log.Info($"[{CompanySupplier.ModName}] Werft-Lager-Kapazitaet x{factor} auf {count} Tier(s) gesetzt.");
        }

        /// <summary>Setzt die Lager-Kapazitaet aller Werft-Tiers auf die gesnapshotteten Originalwerte zurueck.</summary>
        public void ResetShipyardCapacity()
        {
            foreach (var proto in GetShipyards())
            {
                string id = proto.Id.ToString();
                if (!_origShipyardCap.TryGetValue(id, out int orig)) continue;
                try { WriteShipyardCapacity(proto, orig); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ResetShipyardCapacity({proto?.Id}): {ex.Message}"); }
            }
            _origShipyardCap.Clear();
            Log.Info($"[{CompanySupplier.ModName}] Werft-Lager-Kapazitaet zurueckgesetzt.");
        }

        // Schreibt das (readonly) Proto-Feld ShipyardProto.CargoCapacity per Reflection (wie CapacityMultiplier).
        private static void WriteShipyardCapacity(ShipyardProto proto, int value)
        {
            FieldInfo fi = proto.GetType().GetField("CargoCapacity", InstAll);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Feld 'CargoCapacity' auf {proto.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            fi.SetValue(proto, new Quantity(value));
        }

        // Schreibt das (readonly) Proto-Feld CargoShipProto.CapacityMultiplier per Reflection.
        // SetValue funktioniert auf .NET 4.8 auch fuer initOnly-Instanzfelder (vgl. VehicleStatsCheats).
        private static void WriteCapacityField(CargoShipProto proto, Percent value)
        {
            FieldInfo fi = proto.GetType().GetField("CapacityMultiplier", InstAll);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Feld 'CapacityMultiplier' auf {proto.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            fi.SetValue(proto, value);
        }
    }
}
