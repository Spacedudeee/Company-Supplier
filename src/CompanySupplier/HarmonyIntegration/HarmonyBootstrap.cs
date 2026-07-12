using System;
using System.IO;
using System.Reflection;
using Mafi;

namespace CompanySupplier.HarmonyIntegration
{
    /// <summary>
    /// Bootstrappt Harmony fuer die Pipe-Build-Cheats — BRUCHSICHER: schlaegt irgendetwas fehl (0Harmony
    /// nicht ladbar, Patch-Ziel weg durch API-Drift), bleibt NUR der Pipe-Cheat inaktiv; der restliche Mod
    /// (alle anderen Cheats) laeuft unveraendert weiter.
    ///
    /// Warum ein eigener AssemblyResolve statt sich auf den Mod-Ordner-Probe-Pfad zu verlassen: CoI-Mods
    /// nutzen ueblicherweise KEIN Harmony, daher ist nicht garantiert, dass die Laufzeit <c>0Harmony.dll</c>
    /// aus dem Mod-Ordner findet. Der Handler laedt sie explizit von dort (neben der Mod-DLL).
    ///
    /// Reihenfolge ist wichtig: der Resolver wird ZUERST registriert; erst DANACH wird
    /// <see cref="ApplyPatches"/> aufgerufen — eine separate Methode, damit die HarmonyLib-Typen erst bei
    /// IHREM JIT (also nach dem Registrieren des Resolvers) aufgeloest werden.
    /// </summary>
    internal static class HarmonyBootstrap
    {
        private const string HarmonyId = "companysupplier.pipes";
        private static bool _initialized;

        /// <summary>True, wenn Harmony erfolgreich geladen und die Patches angewandt wurden (fuer die UI:
        /// die Pipe-Toggles bleiben ohne Harmony wirkungslos).</summary>
        public static bool Active { get; private set; }

        /// <param name="modDir">Mod-Wurzelverzeichnis aus dem Manifest (<c>ModManifest.RootDirectoryPath</c>).
        /// <c>Assembly.Location</c> taugt NICHT: bei <c>non_locking_dll_load</c> wird die DLL aus Bytes geladen
        /// und Location ist leer (fuehrte zu „Invalid path"). Fallback: Standard-Mods-Ordner + Assembly-Name.</param>
        public static void TryInit(string modDir)
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
                    modDir = FallbackModDir();
                if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
                {
                    Log.Warning($"[{CompanySupplier.ModName}] Harmony-Init: Mod-Ordner nicht gefunden — Pipe-Cheats inaktiv.");
                    return;
                }

                string harmonyDll = Path.Combine(modDir, "0Harmony.dll");
                if (!File.Exists(harmonyDll))
                {
                    Log.Warning($"[{CompanySupplier.ModName}] Harmony-Init: 0Harmony.dll nicht in '{modDir}' — Pipe-Cheats inaktiv.");
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var requested = new AssemblyName(args.Name);
                        if (requested.Name == "0Harmony" && File.Exists(harmonyDll))
                            return Assembly.LoadFrom(harmonyDll);
                    }
                    catch { /* Resolver darf nie werfen */ }
                    return null;
                };

                ApplyPatches();
                Active = true;
                Log.Info($"[{CompanySupplier.ModName}] Harmony aktiv — Pipe-Cheats geladen (0Harmony aus '{modDir}').");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Harmony-Init fehlgeschlagen — Pipe-Cheats inaktiv, restlicher Mod laeuft: {ex.Message}");
            }
        }

        /// <summary>Fallback-Mod-Ordner: <c>%APPDATA%\Captain of Industry\Mods\&lt;AssemblyName&gt;</c>.
        /// <c>GetName().Name</c> funktioniert auch bei aus Bytes geladenen Assemblies (anders als Location).</summary>
        private static string FallbackModDir()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string asmName = Assembly.GetExecutingAssembly().GetName().Name;
                return Path.Combine(appData, "Captain of Industry", "Mods", asmName);
            }
            catch { return null; }
        }

        // Separate Methode: referenziert HarmonyLib-Typen, die erst hier (nach Resolver-Registrierung) JITen.
        private static void ApplyPatches()
        {
            var harmony = new HarmonyLib.Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
