using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.Trucks;
using Mafi.Core.Vehicles.Excavators;
using Mafi.Core.Entities.Dynamic;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider: exakte Stats PRO Fahrzeugtyp (zunaechst Ladekapazitaet; Geschwindigkeit folgt separat).
    ///
    /// Mechanik: Fahrzeug-Stats stecken in (readonly) Proto-Feldern — es gibt KEINE globale Per-Typ-Property
    /// (anders als der globale <c>TrucksCapacityMultiplier</c> in <see cref="FleetVehicleCheats"/>, der ALLE
    /// LKW gleich skaliert). Fuer einen exakten Wert pro Typ ueberschreiben wir das Proto-Feld per Reflection
    /// und ziehen die bereits gespawnten Fahrzeuge nach.
    ///
    /// Lebensdauer/Reset: Protos werden je Sitzung aus den Spieldaten neu gebaut (NICHT im Savegame), daher
    /// snapshotten wir den Originalwert je Typ LAZY beim ersten Edit (capture-on-first-edit) und stellen ihn
    /// beim Reset wieder her. ACHTUNG: lebende Fahrzeug-Instanzen werden serialisiert — ein Reset setzt daher
    /// auch die Live-Kapazitaet aktiv zurueck.
    ///
    /// Robustheit (Early-Access-Interna koennen driften): jeder Reflection-Zugriff in try/catch + Log.
    /// </summary>
    public sealed class VehicleStatsCheats
    {
        private const BindingFlags InstAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly DependencyResolver _resolver;
        private IVehiclesManager _vehiclesManager;

        // Original-Ladekapazitaet je Proto-Id (gesnapshottet VOR dem ersten Override) -> ermoeglicht Reset.
        private readonly Dictionary<string, int> _origCapacity = new Dictionary<string, int>();

        // Original-Vorwaertsgeschwindigkeit (Tiles/Sek) je Proto-Id (gesnapshottet VOR dem ersten Override).
        private readonly Dictionary<string, double> _origSpeed = new Dictionary<string, double>();

        public VehicleStatsCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IVehiclesManager>(out _vehiclesManager);
        }

        /// <summary>Hat dieser Fahrzeugtyp ueberhaupt eine Ladekapazitaet? (LKW/Bagger ja, Rakete nein.)</summary>
        public bool HasCapacity(DrivingEntityProto proto)
            => proto is TruckProto || proto is ExcavatorProto;

        /// <summary>Aktuelle (effektive) Ladekapazitaet des Typs, oder -1 wenn der Typ keine hat.</summary>
        public int GetCapacity(DrivingEntityProto proto)
        {
            try
            {
                if (proto is TruckProto t) return t.CapacityBase.Value;
                if (proto is ExcavatorProto e) return e.Capacity.Value;
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] GetCapacity({proto?.Id}): {ex.Message}"); }
            return -1;
        }

        /// <summary>Original-(Default-)Kapazitaet: der gesnapshottete Wert, sonst der aktuelle (= unveraendert).</summary>
        public int GetDefaultCapacity(DrivingEntityProto proto)
        {
            if (proto != null && _origCapacity.TryGetValue(proto.Id.ToString(), out int orig)) return orig;
            return GetCapacity(proto);
        }

        /// <summary>True, wenn dieser Typ aktuell einen aktiven Kapazitaets-Override hat (fuer die UI).</summary>
        public bool HasCapacityOverride(DrivingEntityProto proto)
            => proto != null && _origCapacity.ContainsKey(proto.Id.ToString());

        /// <summary>Setzt die Ladekapazitaet des Typs EXAKT (geklemmt &gt;= 1). Snapshottet vorher den Originalwert
        /// und zieht bereits gespawnte Fahrzeuge dieses Typs nach.</summary>
        public void SetCapacity(DrivingEntityProto proto, int value)
        {
            if (proto == null || !HasCapacity(proto)) return;
            if (value < 1) value = 1;
            try
            {
                SnapshotCapacity(proto);
                WriteCapacityField(proto, value);
                UpdateLiveCapacity(proto, value);
                Log.Info($"[{CompanySupplier.ModName}] Kapazitaet {proto.Id} = {value} gesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetCapacity({proto.Id}): {ex.Message}"); }
        }

        /// <summary>Setzt die Kapazitaet des Typs auf den gesnapshotteten Originalwert zurueck (no-op, wenn nie veraendert).</summary>
        public void ResetCapacity(DrivingEntityProto proto)
        {
            if (proto == null || !_origCapacity.TryGetValue(proto.Id.ToString(), out int orig)) return;
            try
            {
                WriteCapacityField(proto, orig);
                UpdateLiveCapacity(proto, orig);
                _origCapacity.Remove(proto.Id.ToString());
                Log.Info($"[{CompanySupplier.ModName}] Kapazitaet {proto.Id} auf {orig} zurueckgesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ResetCapacity({proto.Id}): {ex.Message}"); }
        }

        private void SnapshotCapacity(DrivingEntityProto proto)
        {
            string id = proto.Id.ToString();
            if (!_origCapacity.ContainsKey(id)) _origCapacity[id] = GetCapacity(proto);
        }

        // Schreibt das readonly Proto-Kapazitaetsfeld (TruckProto.CapacityBase / ExcavatorProto.Capacity) per
        // Reflection. SetValue funktioniert auf .NET 4.8 auch fuer initOnly-Instanzfelder.
        private static void WriteCapacityField(DrivingEntityProto proto, int value)
        {
            string fieldName = proto is TruckProto ? "CapacityBase" : "Capacity";
            FieldInfo fi = proto.GetType().GetField(fieldName, InstAll);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Kapazitaetsfeld '{fieldName}' auf {proto.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            fi.SetValue(proto, new Quantity(value));
        }

        // Zieht bereits gespawnte Fahrzeuge dieses Typs nach: setzt deren Capacity direkt ueber den privaten
        // Setter (set_Capacity). Bei Typen ohne diesen Setter (z. B. Bagger) wirkt der Proto-Wert erst auf
        // neu gespawnte Instanzen — best effort, wird je Fahrzeug isoliert gekapselt.
        private void UpdateLiveCapacity(DrivingEntityProto proto, int value)
        {
            if (_vehiclesManager == null) return;
            var q = new Quantity(value);
            foreach (Vehicle v in _vehiclesManager.AllVehicles)
            {
                try
                {
                    if (v?.Prototype == null || v.Prototype.Id != proto.Id) continue;
                    MethodInfo setCap = v.GetType().GetMethod("set_Capacity", InstAll);
                    setCap?.Invoke(v, new object[] { q });
                }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] UpdateLiveCapacity({v?.Prototype?.Id}): {ex.Message}"); }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Geschwindigkeit (Reflection auf DrivingData; lebende Fahrzeuge via SmoothDriver) — best effort
        // ------------------------------------------------------------------------------------------

        /// <summary>Hat dieser Typ eine (lesbare) Geschwindigkeit? Alle DrivingEntityProto fuehren DrivingData.</summary>
        public bool HasSpeed(DrivingEntityProto proto) => proto?.DrivingData != null;

        /// <summary>Aktuelle Max-Vorwaertsgeschwindigkeit (Tiles/Sekunde), oder -1 wenn nicht lesbar.</summary>
        public double GetSpeed(DrivingEntityProto proto)
        {
            try
            {
                var dd = proto?.DrivingData;
                if (dd == null) return -1;
                return dd.MaxForwardsSpeed.Value.ToDouble();
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] GetSpeed({proto?.Id}): {ex.Message}"); return -1; }
        }

        /// <summary>Original-(Default-)Geschwindigkeit: der gesnapshottete Wert, sonst der aktuelle.</summary>
        public double GetDefaultSpeed(DrivingEntityProto proto)
        {
            if (proto != null && _origSpeed.TryGetValue(proto.Id.ToString(), out double o)) return o;
            return GetSpeed(proto);
        }

        /// <summary>Setzt die Max-Vorwaertsgeschwindigkeit (Tiles/Sek). Geklemmt auf [0.1 .. max(10x Default, 5)]
        /// gegen Physik-/Pathfinding-Brueche. Snapshottet vorher den Originalwert und zieht lebende Fahrzeuge nach.</summary>
        public void SetSpeed(DrivingEntityProto proto, double tilesPerSec)
        {
            if (proto?.DrivingData == null) return;
            try
            {
                SnapshotSpeed(proto);
                double def = GetDefaultSpeed(proto);
                double maxAllowed = Math.Max(def * 10.0, 5.0);
                if (tilesPerSec > maxAllowed) tilesPerSec = maxAllowed;
                if (tilesPerSec < 0.1) tilesPerSec = 0.1;
                WriteSpeedField(proto, tilesPerSec);
                UpdateLiveSpeed(proto, tilesPerSec);
                Log.Info($"[{CompanySupplier.ModName}] Geschwindigkeit {proto.Id} = {tilesPerSec:0.##} gesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetSpeed({proto.Id}): {ex.Message}"); }
        }

        /// <summary>Setzt die Geschwindigkeit des Typs auf den gesnapshotteten Originalwert zurueck.</summary>
        public void ResetSpeed(DrivingEntityProto proto)
        {
            if (proto == null || !_origSpeed.TryGetValue(proto.Id.ToString(), out double o)) return;
            try
            {
                WriteSpeedField(proto, o);
                UpdateLiveSpeed(proto, o);
                _origSpeed.Remove(proto.Id.ToString());
                Log.Info($"[{CompanySupplier.ModName}] Geschwindigkeit {proto.Id} auf {o:0.##} zurueckgesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ResetSpeed({proto.Id}): {ex.Message}"); }
        }

        private void SnapshotSpeed(DrivingEntityProto proto)
        {
            string id = proto.Id.ToString();
            if (!_origSpeed.ContainsKey(id)) _origSpeed[id] = GetSpeed(proto);
        }

        // Schreibt das readonly DrivingData.MaxForwardsSpeed (RelTile1f) per Reflection.
        private static void WriteSpeedField(DrivingEntityProto proto, double tilesPerSec)
        {
            object dd = proto.DrivingData;
            if (dd == null) return;
            FieldInfo fi = dd.GetType().GetField("MaxForwardsSpeed", InstAll);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Feld 'MaxForwardsSpeed' auf {dd.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            fi.SetValue(dd, RelTile1f.FromTilesPerSecond(tilesPerSec));
        }

        // Zieht lebende Fahrzeuge dieses Typs nach: ueberschreibt die privaten Basis-Geschwindigkeitsfelder im
        // per-Instanz SmoothDriver und ruft StartUpdate(). Best effort + isoliert je Fahrzeug (private Interna).
        private void UpdateLiveSpeed(DrivingEntityProto proto, double tilesPerSec)
        {
            if (_vehiclesManager == null) return;
            Fix32 speed = RelTile1f.FromTilesPerSecond(tilesPerSec).Value;
            foreach (Vehicle v in _vehiclesManager.AllVehicles)
            {
                try
                {
                    if (v?.Prototype == null || v.Prototype.Id != proto.Id) continue;
                    object sd = FindField(v.GetType(), "m_speedDriver")?.GetValue(v);
                    if (sd == null) continue;
                    SetField(sd, "m_maxForwardsSpeedBase", speed);
                    SetField(sd, "m_maxForwardsSpeed", speed);
                    sd.GetType().GetMethod("StartUpdate", InstAll)?.Invoke(sd, null);
                }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] UpdateLiveSpeed({v?.Prototype?.Id}): {ex.Message}"); }
            }
        }

        // -- Reflection-Helfer ---------------------------------------------------------------------

        /// <summary>Sucht ein Instanzfeld ueber die gesamte Vererbungskette (private Felder einer Basisklasse
        /// findet ein einzelnes GetField sonst NICHT).</summary>
        private static FieldInfo FindField(Type t, string name)
        {
            for (Type cur = t; cur != null; cur = cur.BaseType)
            {
                FieldInfo f = cur.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo fi = FindField(target.GetType(), name);
            fi?.SetValue(target, value);
        }
    }
}
