using System;
using System.Reflection;
using Mafi;
using Mafi.Core.MessageNotifications;
using Mafi.Core.Research;

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
    public sealed class ResearchCheats
    {
        private readonly DependencyResolver _resolver;
        private ResearchManager _research;
        private IMessageNotificationsManager _notifications;

        public ResearchCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<ResearchManager>(out _research);
            _resolver.TryResolve<IMessageNotificationsManager>(out _notifications);
        }

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
    }
}
