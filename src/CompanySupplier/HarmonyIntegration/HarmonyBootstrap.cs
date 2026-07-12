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

        public static void TryInit()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                string modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(modDir))
                {
                    Log.Warning($"[{CompanySupplier.ModName}] Harmony-Init: Mod-Verzeichnis nicht ermittelbar — Pipe-Cheats inaktiv.");
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var requested = new AssemblyName(args.Name);
                        if (requested.Name == "0Harmony")
                        {
                            string dll = Path.Combine(modDir, "0Harmony.dll");
                            if (File.Exists(dll)) return Assembly.LoadFrom(dll);
                        }
                    }
                    catch { /* Resolver darf nie werfen */ }
                    return null;
                };

                ApplyPatches();
                Active = true;
                Log.Info($"[{CompanySupplier.ModName}] Harmony aktiv — Pipe-Cheats geladen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Harmony-Init fehlgeschlagen — Pipe-Cheats inaktiv, restlicher Mod laeuft: {ex.Message}");
            }
        }

        // Separate Methode: referenziert HarmonyLib-Typen, die erst hier (nach Resolver-Registrierung) JITen.
        private static void ApplyPatches()
        {
            var harmony = new HarmonyLib.Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
