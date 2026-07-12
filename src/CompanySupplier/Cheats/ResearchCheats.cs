using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Collections.ImmutableCollections;   // ImmutableArray (Parents leeren)
using Mafi.Core.MessageNotifications;
using Mafi.Core.Prototypes;                    // ProtosDb, UnlockedProtosDb, Proto
using Mafi.Core.Research;
using Mafi.Core.Simulation;                    // ICalendar (Tages-Hook fuer Ignore-Toggles)

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Forschungs-Cheats: laufende Forschung sofort abschliessen oder den kompletten Forschungsbaum
    /// freischalten.
    ///
    /// 0.8.5.0-Hinweise (per inspect.ps1 verifiziert gegen Mafi.Core.dll):
    ///  - ResearchManager.Cheat_FinishCurrent()      ist [public]   -> Direktaufruf.
    ///  - ResearchManager.Cheat_UnlockAllResearch()  ist [internal] -> Aufruf per Reflection.
    ///  - IMessageNotificationsManager.DismissAllNotifications() ist [public]; nach dem
    ///    Massen-Unlock raeumen wir die Flut an "Forschung abgeschlossen"-Meldungen weg
    ///    (wie der April-2024-Referenzprovider, aber mit neu verifizierter API).
    ///
    /// Der alte Referenz-Code lief fuer "Finish" ueber IInputScheduler + ResearchCheatFinishCmd;
    /// in 0.8.5.0 ist dafuer die public Cheat_FinishCurrent()-Methode da, daher kein InputScheduler
    /// mehr noetig.
    /// </summary>
    public sealed class ResearchCheats : IEventOwner
    {
        private readonly DependencyResolver _resolver;
        private ResearchManager _research;
        private IMessageNotificationsManager _notifications;
        private ProtosDb _protos;                 // WorldGen-Unlocks (Saatgut/Radar)
        private UnlockedProtosDb _unlocked;        // WorldGen-Unlocks: Proto freischalten
        private ICalendar _calendar;              // Tages-Hook fuer die Ignore-Requirement-Toggles

        /// <summary>true = Forschungs-Voraussetzungen (benoetigte Produkte/Bedingungen) werden ignoriert.</summary>
        public bool IgnoreItemRequirements { get; private set; }

        /// <summary>true = Eltern-Voraussetzungen im Forschungsbaum werden ignoriert (jeder Knoten forschbar).</summary>
        public bool IgnoreParentRequirements { get; private set; }

        // Original-Eltern je Knoten-Id (gesnapshottet, um IgnoreParentRequirements sauber zurueckzunehmen).
        private readonly Dictionary<string, ImmutableArray<ResearchNode>> _origParents
            = new Dictionary<string, ImmutableArray<ResearchNode>>();

        // Leeres Eltern-Array (typisiert), das wir per Reflection in node.Parents schreiben.
        private static readonly ImmutableArray<ResearchNode> EmptyParents
            = ImmutableArray.CreateRange(System.Array.Empty<ResearchNode>());

        public ResearchCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<ResearchManager>(out _research);
            _resolver.TryResolve<IMessageNotificationsManager>(out _notifications);
            _resolver.TryResolve<ProtosDb>(out _protos);
            _resolver.TryResolve<UnlockedProtosDb>(out _unlocked);

            if (_resolver.TryResolve<ICalendar>(out _calendar) && _calendar != null)
            {
                try { _calendar.NewDay.AddNonSaveable(this, OnNewDay); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ResearchCheats NewDay-Abo fehlgeschlagen: {ex.Message}"); }
            }
        }

        /// <summary><see cref="IEventOwner"/>: der Provider lebt so lange wie der Mod, wird nie zerstoert.</summary>
        public bool IsDestroyed => false;

        /// <summary>Schliesst die aktuell laufende Forschung sofort ab. No-op, wenn nichts laeuft.</summary>
        public void FinishCurrentResearch()
        {
            if (_research == null) return;
            try
            {
                _research.Cheat_FinishCurrent();
                Log.Info($"[{CompanySupplier.ModName}] Laufende Forschung abgeschlossen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] FinishCurrentResearch: {ex.Message}");
            }
        }

        /// <summary>Schaltet den gesamten Forschungsbaum frei. Cheat_UnlockAllResearch ist internal,
        /// daher per Reflection aufgerufen. Danach werden die ausgeloesten Notifications weggeraeumt.</summary>
        public void UnlockAllResearch()
        {
            if (_research == null) return;
            try
            {
                var mi = typeof(ResearchManager).GetMethod(
                    "Cheat_UnlockAllResearch",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (mi == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] UnlockAllResearch: Methode Cheat_UnlockAllResearch nicht gefunden.");
                    return;
                }
                mi.Invoke(_research, null);

                // Berg an "Forschung abgeschlossen"-Meldungen wegraeumen.
                _notifications?.DismissAllNotifications();

                Log.Info($"[{CompanySupplier.ModName}] Gesamte Forschung freigeschaltet.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] UnlockAllResearch: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Cheat++-Paritaet: verfuegbare/wiederholbare Forschung + Voraussetzungen ignorieren
        // ----------------------------------------------------------------------------------------

        /// <summary>Schaltet alle AKTUELL verfuegbaren (nicht gesperrten, noch nicht erforschten) Knoten frei —
        /// im Gegensatz zu <see cref="UnlockAllResearch"/> nur die erreichbare "Front", nicht der ganze Baum.</summary>
        public void UnlockAvailableResearch()
        {
            if (_research?.AllNodes == null) return;
            var dict = new Dictionary<string, int>();
            foreach (var node in _research.AllNodes)
            {
                if (node?.Proto == null) continue;
                if (node.State == ResearchNodeState.NotResearched && !node.IsLocked)
                    dict[node.Proto.Id.ToString()] = 1;
            }
            InvokeUnlockNodes(dict, "Verfuegbare Forschung");
        }

        /// <summary>Schaltet alle WIEDERHOLBAREN Forschungsknoten (<c>MaxResearchCount &gt; 1</c>) einmal frei.
        /// Bewusst nur EIN Level je Knoten, damit die inkrementellen Boni nicht absurd stacken/ueberlaufen.</summary>
        public void UnlockRepeatableResearch()
        {
            if (_research?.AllNodes == null) return;
            var dict = new Dictionary<string, int>();
            foreach (var node in _research.AllNodes)
            {
                if (node?.Proto == null) continue;
                if (node.Proto.MaxResearchCount > 1)
                    dict[node.Proto.Id.ToString()] = 1;
            }
            InvokeUnlockNodes(dict, "Wiederholbare Forschung");
        }

        /// <summary>Schaltet "Produkt-/Bedingungs-Voraussetzungen ignorieren" ein/aus. Solange aktiv, werden die
        /// Freischalt-Bedingungen (<c>LockedByConditions</c>) noch nicht erforschter Knoten taeglich geleert.
        /// Die Bedingungen werden beim Spielstand-Laden aus den Protos neu aufgebaut (kein Save-Schaden).</summary>
        public void SetIgnoreItemRequirements(bool enabled)
        {
            IgnoreItemRequirements = enabled;
            if (enabled) ApplyIgnoreItemRequirements();
            Log.Info($"[{CompanySupplier.ModName}] Forschungs-Voraussetzungen ignorieren = {enabled}.");
        }

        private void ApplyIgnoreItemRequirements()
        {
            if (_research?.AllNodes == null) return;
            try
            {
                foreach (var node in _research.AllNodes)
                {
                    if (node == null || node.State == ResearchNodeState.Researched) continue;
                    node.LockedByConditions?.Clear();
                }
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ApplyIgnoreItemRequirements: {ex.Message}"); }
        }

        /// <summary>Schaltet "Eltern-Voraussetzungen ignorieren" ein/aus. Bei "an" werden die Eltern jedes Knotens
        /// (nach Snapshot) geleert -> <c>GetIsLockedByParents()</c> wird vacuously false. Bei "aus" werden die
        /// gesnapshotteten Original-Eltern wiederhergestellt.</summary>
        public void SetIgnoreParentRequirements(bool enabled)
        {
            IgnoreParentRequirements = enabled;
            if (enabled) ApplyIgnoreParentRequirements();
            else RestoreParents();
            Log.Info($"[{CompanySupplier.ModName}] Eltern-Voraussetzungen ignorieren = {enabled}.");
        }

        private void ApplyIgnoreParentRequirements()
        {
            if (_research?.AllNodes == null) return;
            try
            {
                foreach (var node in _research.AllNodes)
                {
                    if (node?.Proto == null) continue;
                    string id = node.Proto.Id.ToString();
                    if (!_origParents.ContainsKey(id)) _origParents[id] = node.Parents;
                    // Parents hat einen non-public Setter (0.8.5.0) -> per Reflection leeren.
                    SetNonPublicProperty(node, "Parents", EmptyParents);
                }
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ApplyIgnoreParentRequirements: {ex.Message}"); }
        }

        private void RestoreParents()
        {
            if (_research?.AllNodes != null)
            {
                try
                {
                    foreach (var node in _research.AllNodes)
                    {
                        if (node?.Proto == null) continue;
                        if (_origParents.TryGetValue(node.Proto.Id.ToString(), out var orig))
                            SetNonPublicProperty(node, "Parents", orig);
                    }
                }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] RestoreParents: {ex.Message}"); }
            }
            _origParents.Clear();
        }

        // ----------------------------------------------------------------------------------------
        // WorldGen-Fixes: durch einen Weltgenerierungs-Bug gesperrte Protos freischalten (Saatgut/Radar)
        // ----------------------------------------------------------------------------------------

        /// <summary>Schaltet die durch den bekannten WorldGen-Bug gesperrten Protos frei (Zuckerrohr/Mohn/Mais-
        /// Saatgut + Schiffs-Radar 2). Nutzt <c>UnlockedProtosDb.Unlock</c>; idempotent (bereits Freies wird
        /// uebersprungen). Nischen-Fix — hilft nur betroffenen Spielstaenden.</summary>
        public void UnlockWorldgenFixes()
        {
            if (_unlocked == null || _protos == null) return;
            try
            {
                var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SugarCane", "Poppy", "Corn" };
                int count = 0;
                foreach (var p in _protos.Filter<Proto>(_ => true))
                {
                    if (p == null) continue;
                    string id = p.Id.ToString();
                    bool hit = exact.Contains(id) || id.IndexOf("ShipRadar", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit || _unlocked.IsUnlocked(p)) continue;
                    _unlocked.Unlock(p);
                    count++;
                }
                _notifications?.DismissAllNotifications();
                Log.Info($"[{CompanySupplier.ModName}] WorldGen-Fixes: {count} Proto(s) freigeschaltet.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] UnlockWorldgenFixes: {ex.Message}"); }
        }

        // ----------------------------------------------------------------------------------------
        // Interna
        // ----------------------------------------------------------------------------------------

        private void OnNewDay()
        {
            if (IgnoreItemRequirements) ApplyIgnoreItemRequirements();
            if (IgnoreParentRequirements) ApplyIgnoreParentRequirements();
        }

        /// <summary>Ruft das internal <c>ResearchManager.UnlockResearchNodes(IReadOnlyDictionary&lt;string,int&gt;)</c>
        /// per Reflection auf (Knoten-Id -&gt; Anzahl Forschungen) und raeumt danach die Notification-Flut weg.</summary>
        private void InvokeUnlockNodes(Dictionary<string, int> nodeTimes, string label)
        {
            if (_research == null || nodeTimes == null || nodeTimes.Count == 0) return;
            try
            {
                var mi = typeof(ResearchManager).GetMethod("UnlockResearchNodes",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (mi == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] {label}: Methode UnlockResearchNodes nicht gefunden.");
                    return;
                }
                mi.Invoke(_research, new object[] { nodeTimes });
                _notifications?.DismissAllNotifications();
                Log.Info($"[{CompanySupplier.ModName}] {label}: {nodeTimes.Count} Knoten freigeschaltet.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] {label}: {ex.Message}"); }
        }

        /// <summary>Setzt eine Property mit non-public Setter per Reflection (fuer <c>ResearchNode.Parents</c>).</summary>
        private static void SetNonPublicProperty(object target, string property, object value)
        {
            if (target == null) return;
            var pi = target.GetType().GetProperty(property,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var setter = pi?.GetSetMethod(nonPublic: true);
            if (setter == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Setter fuer {property} nicht gefunden.");
                return;
            }
            setter.Invoke(target, new[] { value });
        }
    }
}
