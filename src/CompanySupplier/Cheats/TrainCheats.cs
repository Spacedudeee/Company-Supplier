using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Trains;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider: exakte Kapazitaet PRO Zug-Cargo-Waggon-Typ.
    ///
    /// <c>CargoWagonProto</c> liegt im BASIS-Spiel (Mafi.Core.Trains) — KEINE DLC-Referenz noetig. Der
    /// DLC-Molten-Waggon erbt davon und ist automatisch dabei, FALLS das Zug-DLC geladen ist; sonst liefert
    /// die Enumeration einfach keine/weniger Waggons (die UI blendet den Abschnitt dann aus).
    ///
    /// Mechanik: Die Kapazitaet steckt im readonly Proto. Das Spiel rechnet
    /// <c>Capacity = m_baseCapacity * TrainsCapacityMultiplier</c>. Wir ueberschreiben daher BEIDE Felder per
    /// Reflection (nur das Capacity-Backing-Field zu setzen wuerde von der naechsten Neuberechnung geklobbert).
    /// Vor dem ersten Edit wird der Originalwert je Waggon gesnapshottet (Protos werden je Sitzung neu gebaut,
    /// also nicht im Savegame) -> Reset stellt ihn wieder her.
    ///
    /// Robustheit: jeder Reflection-Zugriff in try/catch + Log (private/readonly Interna koennen driften).
    /// </summary>
    public sealed class TrainCheats
    {
        private readonly DependencyResolver _resolver;

        // Original-Kapazitaet je Waggon-Proto-Id (gesnapshottet VOR dem ersten Override).
        private readonly Dictionary<string, int> _origCapacity = new Dictionary<string, int>();

        public TrainCheats(DependencyResolver resolver) => _resolver = resolver;

        /// <summary>Aktuelle Kapazitaet des Waggon-Typs, oder -1 wenn nicht lesbar.</summary>
        public int GetCapacity(CargoWagonProto wagon)
        {
            try { return wagon?.Capacity.Value ?? -1; }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Train.GetCapacity({wagon?.Id}): {ex.Message}"); return -1; }
        }

        /// <summary>Original-(Default-)Kapazitaet: der gesnapshottete Wert, sonst der aktuelle.</summary>
        public int GetDefaultCapacity(CargoWagonProto wagon)
        {
            if (wagon != null && _origCapacity.TryGetValue(wagon.Id.ToString(), out int o)) return o;
            return GetCapacity(wagon);
        }

        /// <summary>True, wenn dieser Waggon-Typ aktuell einen aktiven Override hat (fuer die UI).</summary>
        public bool HasOverride(CargoWagonProto wagon)
            => wagon != null && _origCapacity.ContainsKey(wagon.Id.ToString());

        /// <summary>Setzt die Kapazitaet des Waggon-Typs EXAKT (geklemmt &gt;= 1). Schreibt Backing-Field UND
        /// m_baseCapacity. Snapshottet vorher den Originalwert.</summary>
        public void SetCapacity(CargoWagonProto wagon, int value)
        {
            if (wagon == null) return;
            if (value < 1) value = 1;
            try
            {
                Snapshot(wagon);
                WriteCapacity(wagon, value);
                Log.Info($"[{CompanySupplier.ModName}] Waggon-Kapazitaet {wagon.Id} = {value} gesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Train.SetCapacity({wagon.Id}): {ex.Message}"); }
        }

        /// <summary>Setzt die Kapazitaet des Waggon-Typs auf den gesnapshotteten Originalwert zurueck.</summary>
        public void ResetCapacity(CargoWagonProto wagon)
        {
            if (wagon == null || !_origCapacity.TryGetValue(wagon.Id.ToString(), out int o)) return;
            try
            {
                WriteCapacity(wagon, o);
                _origCapacity.Remove(wagon.Id.ToString());
                Log.Info($"[{CompanySupplier.ModName}] Waggon-Kapazitaet {wagon.Id} auf {o} zurueckgesetzt.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Train.ResetCapacity({wagon.Id}): {ex.Message}"); }
        }

        private void Snapshot(CargoWagonProto wagon)
        {
            string id = wagon.Id.ToString();
            if (!_origCapacity.ContainsKey(id)) _origCapacity[id] = GetCapacity(wagon);
        }

        // Schreibt sowohl das Capacity-Backing-Field als auch m_baseCapacity (Basis fuer die Neuberechnung).
        private static void WriteCapacity(CargoWagonProto wagon, int value)
        {
            var q = new Quantity(value);
            SetField(wagon, "<Capacity>k__BackingField", q);
            SetField(wagon, "m_baseCapacity", q);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo fi = FindField(target.GetType(), name);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Feld '{name}' auf {target.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            fi.SetValue(target, value);
        }

        // Sucht ein Instanzfeld ueber die Vererbungskette (Capacity/m_baseCapacity sind auf der Basisklasse
        // CargoWagonProto privat -> ein einzelnes GetField auf dem konkreten Waggon-Typ faende sie nicht).
        private static FieldInfo FindField(Type t, string name)
        {
            for (Type cur = t; cur != null; cur = cur.BaseType)
            {
                FieldInfo f = cur.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }
    }
}
