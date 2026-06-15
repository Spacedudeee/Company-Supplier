using System;
using System.Reflection;
using Mafi;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Entities;
using Mafi.Core.Utils;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider fuer die Gruppe "Gebaeude/Lager".
    ///
    /// Robustheits-Prinzip (Early-Access-API kann driften): jeder Manager wird mit TryResolve geholt
    /// und jeder Cheat in try/catch gekapselt — ein einzelner gebrochener Cheat darf weder den Mod
    /// noch das Spiel zum Absturz bringen, sondern wird nur ins Log geschrieben.
    /// </summary>
    public sealed class BuildingCheats
    {
        private readonly DependencyResolver _resolver;

        // Verifiziert gegen 0.8.5.0:
        private IEntitiesManager _entities;       // Enumeration aller Lager-Entitaeten im Spiel
        private InstaBuildManager _instaBuild;    // Sofortbau an/aus (public IsInstaBuildEnabled-Setter)

        public BuildingCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IEntitiesManager>(out _entities);
            _resolver.TryResolve<InstaBuildManager>(out _instaBuild);
        }

        /// <summary>
        /// Setzt bei ALLEN Lagern im Spiel den Cheat-Modus:
        /// <see cref="Storage.StorageCheatMode.KeepFull"/> = immer voll / unendlich,
        /// <see cref="Storage.StorageCheatMode.KeepEmpty"/> = wird staendig geleert,
        /// <see cref="Storage.StorageCheatMode.None"/> = aus (Normalbetrieb).
        /// </summary>
        /// <remarks>
        /// 0.8.5.0: Der Setter von <c>Storage.CheatMode</c> ist NICHT public (nur ein non-public Setter,
        /// verifiziert per inspect.ps1 -> CS0200). Gesetzt wird daher ueber die internal Cheat-API
        /// <c>Storage.Cheat_SetCheatMode(StorageCheatMode)</c> per Reflection. Enumeration ueber den
        /// public <c>IEntitiesManager.GetAllEntitiesOfType&lt;Storage&gt;()</c>.
        /// </remarks>
        public void SetAllStoragesGodMode(Storage.StorageCheatMode mode)
        {
            if (_entities == null) return;
            int count = 0;
            try
            {
                MethodInfo setCheatMode = typeof(Storage).GetMethod(
                    "Cheat_SetCheatMode",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (setCheatMode == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] SetAllStoragesGodMode: Storage.Cheat_SetCheatMode nicht gefunden.");
                    return;
                }

                foreach (var storage in _entities.GetAllEntitiesOfType<Storage>())
                {
                    try
                    {
                        setCheatMode.Invoke(storage, new object[] { mode });
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[{CompanySupplier.ModName}] Lager {storage?.Id} God-Mode fehlgeschlagen: {ex.Message}");
                    }
                }
                Log.Info($"[{CompanySupplier.ModName}] Lager-God-Mode = {mode} bei {count} Lagern gesetzt.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetAllStoragesGodMode: {ex.Message}");
            }
        }

        /// <summary>Schaltet den Sofortbau global an/aus (Gebaeude werden ohne Bauzeit/Material fertig).</summary>
        /// <remarks>
        /// 0.8.5.0: Der Setter von <c>InstaBuildManager.IsInstaBuildEnabled</c> ist NICHT public
        /// (verifiziert per inspect.ps1 -> CS0200). Geschaltet wird daher ueber die internal
        /// <c>InstaBuildManager.SetInstaBuild(bool)</c> per Reflection.
        /// </remarks>
        public void SetInstaBuild(bool enabled)
        {
            if (_instaBuild == null) return;
            try
            {
                MethodInfo setInstaBuild = typeof(InstaBuildManager).GetMethod(
                    "SetInstaBuild",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (setInstaBuild == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] SetInstaBuild: InstaBuildManager.SetInstaBuild nicht gefunden.");
                    return;
                }
                setInstaBuild.Invoke(_instaBuild, new object[] { enabled });
                Log.Info($"[{CompanySupplier.ModName}] Sofortbau = {enabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetInstaBuild: {ex.Message}");
            }
        }
    }
}
