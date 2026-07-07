using System;
using System.Collections.Generic;
using Mafi;

namespace CompanySupplier.UI
{
    /// <summary>
    /// Zentrale UI-Ruecksynchronisation (ui-spec Abschnitt 0: Toggles muessen den echten Spielzustand
    /// zeigen). Tabs registrieren hier einmalig im Ctor ihre Sync-Aktion; <see cref="SyncAll"/> zieht
    /// alle Toggle-/Anzeige-Zustaende aus dem Backend nach und wird aufgerufen:
    ///  - nach jedem Fensterbau (<c>CheatMenuWindowView.BuildWindow</c>),
    ///  - nach Bulk-Zustandsaenderungen (<c>CheatService.ApplyState</c> / <c>DisableAllContinuousCheats</c>,
    ///    also Auto-Restore, Presets, Panik-Aus),
    ///  - nach dem (De-)Aktivieren eines Welt-Werkzeugs (auch extern durch den Input-Manager).
    ///
    /// WICHTIG (Lebensdauer): Der DI-Container wird PRO Spielstand-Laden neu gebaut (vgl.
    /// <c>CheatMenuController</c>: "Der Controller wird pro Laden neu gebaut"), also entstehen je Ladevorgang
    /// frische Tab-Instanzen. Die Registry ist daher <b>keyed pro Tab-Typ</b> (nicht append-only): eine neue
    /// Instanz ersetzt die Registrierung der alten desselben Typs. So akkumulieren sich ueber mehrere Ladungen
    /// hinweg keine toten Delegates (die sonst die kompletten alten UI-Baeume + Proto-Listen im Speicher
    /// halten wuerden) und <see cref="SyncAll"/> laeuft nie gegen abgehaengte UI.
    /// </summary>
    internal static class CheatUiSync
    {
        // Ein Slot je Tab-Typ (Key). Ersetzen statt anhaengen -> keine Leichen ueber Save-Loads.
        private static readonly Dictionary<string, Action> _syncs = new Dictionary<string, Action>();

        /// <summary>Registriert (oder ersetzt) die Sync-Aktion eines Tabs. <paramref name="key"/> ist
        /// typstabil (i. d. R. der Tab-Typname) -> eine neu geladene Instanz verdraengt die alte.</summary>
        internal static void Register(string key, Action sync)
        {
            if (string.IsNullOrEmpty(key) || sync == null) return;
            _syncs[key] = sync;
        }

        /// <summary>Zieht alle registrierten UI-Zustaende aus dem Backend nach (best-effort je Tab).
        /// Iteriert ueber eine Momentaufnahme, damit eine (theoretische) Registrierung waehrend des Syncs
        /// die Iteration nicht bricht.</summary>
        internal static void SyncAll()
        {
            Action[] snapshot;
            var values = _syncs.Values;
            snapshot = new Action[values.Count];
            values.CopyTo(snapshot, 0);

            foreach (var sync in snapshot)
            {
                try { sync(); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] UI-Sync fehlgeschlagen: {ex.Message}"); }
            }
        }
    }
}
