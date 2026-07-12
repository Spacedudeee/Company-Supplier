using Mafi;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;

namespace CompanySupplier
{
    /// <summary>
    /// Einstiegspunkt des Cheat-Mods. Erbt von <see cref="DataOnlyMod"/> (implementiert das
    /// gesamte IMod-Boilerplate aus 0.8.5.0: Manifest/JsonConfig/Dispose) und ueberschreibt
    /// nur die fuer die Cheat-Engine relevanten Lifecycle-Methoden.
    ///
    /// P2-Skelett: beweist zunaechst nur, dass der Mod gegen 0.8.5.0 kompiliert, geladen wird
    /// und ins Log schreibt. Die Cheat-Provider werden in P3 in RegisterDependencies registriert.
    /// </summary>
    public sealed class CompanySupplier : DataOnlyMod
    {
        public const string ModName = "CompanySupplier";

        /// <summary>Mod-Wurzelverzeichnis aus dem Manifest — die zuverlaessige Quelle fuer den Ordner, in dem
        /// 0Harmony.dll liegt. WICHTIG: <c>Assembly.Location</c> ist hier LEER, weil das Manifest
        /// <c>non_locking_dll_load</c> setzt (die DLL wird aus Bytes geladen) — daher NICHT darauf verlassen.</summary>
        private readonly string _rootDir;

        public CompanySupplier(ModManifest manifest) : base(manifest)
        {
            _rootDir = manifest?.RootDirectoryPath;
            Log.Info($"[{ModName}] constructed (v0.1.0)");
        }

        /// <summary>Abstrakt in DataOnlyMod -> muss ueberschrieben werden. Cheat-Mod registriert
        /// (vorerst) keine eigenen Prototypen.</summary>
        public override void RegisterPrototypes(ProtoRegistrator registrator)
        {
        }

        /// <summary>In DataOnlyMod sind Initialize/RegisterDependencies 'sealed' — aber EarlyInit ist
        /// 'virtual' und liefert den DI-Resolver. Hier wird die Cheat-Engine eingehaengt.</summary>
        public override void EarlyInit(DependencyResolver resolver)
        {
            base.EarlyInit(resolver);
            // Gekapselt: ein Fehler beim Aufbau der Cheat-Engine (Config/DI/API-Drift) darf niemals
            // das Laden des Spiels bzw. des Spielstands abbrechen — der Mod ist dann eben inaktiv.
            try
            {
                CheatService.Create(resolver);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[{ModName}] CheatService-Aufbau fehlgeschlagen — Mod inaktiv: {ex.Message}");
            }
            // Harmony (Pipe-Cheats) separat + bruchsicher initialisieren: schlaegt es fehl, bleiben nur die
            // Pipe-Cheats aus, alle anderen Cheats laufen weiter. TryInit kapselt seine Fehler selbst.
            // Mod-Wurzelverzeichnis aus dem Manifest durchreichen (Assembly.Location ist bei non_locking_dll_load leer).
            HarmonyIntegration.HarmonyBootstrap.TryInit(_rootDir);
            Log.Info($"[{ModName}] EarlyInit abgeschlossen.");
        }
    }
}
