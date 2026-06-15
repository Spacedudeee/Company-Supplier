using System;
using System.Reflection;
using Mafi;
using Mafi.Core.Buildings.Storages;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider fuer das per-Lager-Welt-Werkzeug ("Lager-Zauberstab"): setzt den Cheat-Modus
    /// EINES angeklickten Lagers, im Gegensatz zu <see cref="BuildingCheats.SetAllStoragesGodMode"/>,
    /// das ALLE Lager auf einmal umstellt.
    ///
    /// Robustheits-Prinzip (Early-Access-API kann driften): der <see cref="MethodInfo"/> wird einmalig
    /// per Reflection aufgeloest und gecacht; jeder Aufruf ist in try/catch gekapselt — ein gebrochener
    /// Cheat darf weder den Mod noch das Spiel zum Absturz bringen, sondern wird nur ins Log geschrieben.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsSelf)]
    public sealed class StorageToolCheats
    {
        // Verifiziert gegen 0.8.5.0 (inspect.ps1):
        //   Storage.CheatMode { get; /* non-public set */ }  -> Getter public lesbar, Setter NICHT public.
        //   internal Storage.Cheat_SetCheatMode(StorageCheatMode) -> Setzen daher per Reflection.
        private readonly MethodInfo _setCheatMode;
        // Cheat-Gegenstueck zu CHEAT_FillToMax: leert ein Lager EINMALIG (forciert, kostenlos), ohne einen
        // Dauer-Modus -> danach laeuft das Lager normal weiter (fuer gezieltes Entsorgen, z. B. Atommuell).
        private readonly MethodInfo _forceClear;

        public StorageToolCheats()
        {
            _setCheatMode = typeof(Storage).GetMethod(
                "Cheat_SetCheatMode",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (_setCheatMode == null)
                Log.Warning($"[{CompanySupplier.ModName}] StorageToolCheats: Storage.Cheat_SetCheatMode nicht gefunden (API-Drift?).");

            _forceClear = typeof(Storage).GetMethod(
                "Cheat_ForceClear",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (_forceClear == null)
                Log.Warning($"[{CompanySupplier.ModName}] StorageToolCheats: Storage.Cheat_ForceClear nicht gefunden (API-Drift?).");
        }

        /// <summary>True, falls die Reflection-Bindung steht — die UI kann damit ihr Werkzeug ggf. sperren.</summary>
        public bool IsAvailable => _setCheatMode != null;

        /// <summary>Setzt den Cheat-Modus EINES Lagers (KeepFull = unendlich liefern,
        /// KeepEmpty = staendig geleert, None = aus).</summary>
        public void SetCheatMode(Storage storage, Storage.StorageCheatMode mode)
        {
            if (storage == null || _setCheatMode == null) return;
            try
            {
                _setCheatMode.Invoke(storage, new object[] { mode });
                Log.Info($"[{CompanySupplier.ModName}] Lager {storage.Id} Cheat-Modus = {mode}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Lager {storage?.Id} Cheat-Modus setzen fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>
        /// Schaltet ein Lager zwischen <paramref name="targetMode"/> und
        /// <see cref="Storage.StorageCheatMode.None"/> um: ist es bereits im Zielmodus, wird es
        /// ausgeschaltet (None), sonst auf den Zielmodus gesetzt. Liefert den neuen Modus zurueck
        /// (oder None, falls nicht setzbar).
        /// </summary>
        public Storage.StorageCheatMode ToggleMode(Storage storage, Storage.StorageCheatMode targetMode)
        {
            if (storage == null) return Storage.StorageCheatMode.None;
            var next = storage.CheatMode == targetMode
                ? Storage.StorageCheatMode.None
                : targetMode;
            SetCheatMode(storage, next);
            return next;
        }

        /// <summary>
        /// Schaltet den Cheat-Modus eines Lagers um: ist es bereits <see cref="Storage.StorageCheatMode.KeepFull"/>,
        /// wird es ausgeschaltet (<see cref="Storage.StorageCheatMode.None"/>), sonst auf KeepFull gesetzt.
        /// Liefert den neuen Modus zurueck (oder den unveraenderten, falls nicht setzbar).
        /// </summary>
        public Storage.StorageCheatMode ToggleKeepFull(Storage storage)
            => ToggleMode(storage, Storage.StorageCheatMode.KeepFull);

        /// <summary>
        /// Leert ein Lager EINMALIG (entfernt den gesamten Inhalt, forciert + kostenlos) und setzt KEINEN
        /// Dauer-Modus — das Lager funktioniert danach normal weiter. Gedacht fuer gezieltes Entsorgen,
        /// z. B. von Atommuell, ohne das Lager (wie KeepEmpty) staendig leer zu halten.
        /// </summary>
        public void ClearStorage(Storage storage)
        {
            if (storage == null || _forceClear == null) return;
            try
            {
                _forceClear.Invoke(storage, null);
                Log.Info($"[{CompanySupplier.ModName}] Lager {storage.Id} geleert (einmalig).");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Lager {storage?.Id} leeren fehlgeschlagen: {ex.Message}");
            }
        }
    }
}
