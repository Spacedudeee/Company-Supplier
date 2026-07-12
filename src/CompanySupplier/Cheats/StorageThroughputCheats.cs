using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core.Buildings.Storages;   // StorageProto : StorageBaseProto (TransferLimit)
using Mafi.Core.Prototypes;           // ProtosDb

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Lager-Durchsatz": skaliert den Transfer-Durchsatz (TransferLimit) ALLER Lager-Typen.
    ///
    /// Mechanik wie <see cref="VehicleStatsCheats"/>/<see cref="ShipCheats"/>: der Durchsatz steckt im
    /// (readonly) Proto-Feld <c>StorageBaseProto.TransferLimit</c> (Quantity) — es gibt KEINE globale Property
    /// und KEIN per-Instanz-Feld (die laufende <c>Storage</c> liest den Proto-Wert live). Wir ueberschreiben
    /// das Feld per Reflection und snapshotten den Originalwert je Typ LAZY beim ersten Edit fuer den Reset.
    ///
    /// Granularitaet: pro Lager-TYP (Proto), nicht pro einzelnem Gebaeude — pro-Gebaeude gaebe es in 0.8.5.0
    /// kein Feld dafuer (das braeuchte Harmony). Ein Faktor deckt alle Typen zugleich ab.
    ///
    /// Robustheit: ProtosDb via TryResolve; jeder Reflection-Zugriff in try/catch + Log.Warning.
    /// </summary>
    public sealed class StorageThroughputCheats
    {
        private const BindingFlags InstAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Obergrenze gegen Overflow: manche Lager haben bereits sehr hohe Default-Limits.
        private const int MaxLimit = int.MaxValue / 4;

        private readonly DependencyResolver _resolver;
        private ProtosDb _protos;

        // Original-TransferLimit je StorageProto-Id (gesnapshottet VOR dem ersten Override) -> Reset.
        private readonly Dictionary<string, int> _origLimit = new Dictionary<string, int>();

        public StorageThroughputCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<ProtosDb>(out _protos);
        }

        /// <summary>Alle Lager-Typen, oder leer wenn die ProtosDb fehlt.</summary>
        private IReadOnlyList<StorageProto> GetStorages()
        {
            if (_protos == null) return Array.Empty<StorageProto>();
            try { return _protos.Filter<StorageProto>(_ => true).ToList(); }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] GetStorages: {ex.Message}"); return Array.Empty<StorageProto>(); }
        }

        /// <summary>True, wenn aktuell ein Durchsatz-Override aktiv ist (fuer die UI-Spiegelung).</summary>
        public bool HasThroughputOverride => _origLimit.Count > 0;

        /// <summary>Skaliert das TransferLimit ALLER Lager-Typen auf das <paramref name="factor"/>-fache des
        /// jeweiligen Originals (capture-on-first-edit je Typ fuer den Reset).</summary>
        public void SetThroughputFactor(int factor)
        {
            if (factor < 1) return;
            int count = 0;
            foreach (var proto in GetStorages())
            {
                try
                {
                    string id = proto.Id.ToString();
                    int orig = proto.TransferLimit.Value;
                    if (orig <= 0) continue;   // 0/unbegrenzt: nichts zu skalieren
                    if (!_origLimit.ContainsKey(id)) _origLimit[id] = orig;
                    long target = (long)_origLimit[id] * factor;
                    if (target > MaxLimit) target = MaxLimit;
                    WriteLimit(proto, (int)target);
                    count++;
                }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetThroughputFactor({proto?.Id}): {ex.Message}"); }
            }
            Log.Info($"[{CompanySupplier.ModName}] Lager-Durchsatz x{factor} auf {count} Typ(en) gesetzt.");
        }

        /// <summary>Setzt das TransferLimit aller Lager-Typen auf die gesnapshotteten Originalwerte zurueck.</summary>
        public void ResetThroughput()
        {
            foreach (var proto in GetStorages())
            {
                string id = proto.Id.ToString();
                if (!_origLimit.TryGetValue(id, out int orig)) continue;
                try { WriteLimit(proto, orig); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ResetThroughput({proto?.Id}): {ex.Message}"); }
            }
            _origLimit.Clear();
            Log.Info($"[{CompanySupplier.ModName}] Lager-Durchsatz zurueckgesetzt.");
        }

        // Schreibt das (readonly) Basisklassen-Feld StorageBaseProto.TransferLimit per Reflection.
        // GetField(Public) findet auch geerbte public Felder ueber die Vererbungskette.
        private static void WriteLimit(StorageProto proto, int value)
        {
            FieldInfo fi = proto.GetType().GetField("TransferLimit", InstAll);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Feld 'TransferLimit' auf {proto.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            fi.SetValue(proto, new Quantity(value));
        }
    }
}
