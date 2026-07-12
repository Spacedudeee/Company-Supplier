using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using Mafi;

namespace CompanySupplier.Config
{
    /// <summary>
    /// Laedt/speichert die <see cref="ModConfig"/> als JSON neben der Mod-DLL
    /// (<c>%APPDATA%\Captain of Industry\Mods\CompanySupplier\cs-config.json</c>). Bewusst best-effort:
    /// jeder Fehler (fehlende Datei, defektes JSON, kein Schreibrecht) wird geloggt und faellt auf
    /// Defaults zurueck — die Config darf das Spiel niemals zum Absturz bringen.
    ///
    /// WICHTIG (Log-Sauberkeit): Die Datei heisst <c>cs-config.json</c>, NICHT <c>config.json</c>. Der
    /// spieleigene <c>DataOnlyMod</c>-Mechanismus (<c>GameBuilder.AttachModJsonConfigs</c>) liest jede
    /// <c>config.json</c> im Mod-Ordner und erwartet SEIN Format — unsere alte gleichnamige Datei loeste
    /// daher bei JEDEM Laden eine Warnung + Stacktrace aus. Mit einem anderen Dateinamen fasst das Spiel
    /// unsere Config nicht mehr an. Eine bestehende alte <c>config.json</c> wird einmalig migriert und entfernt
    /// (siehe <see cref="MigrateLegacyIfNeeded"/>).
    ///
    /// Nutzt <see cref="DataContractJsonSerializer"/> (in .NET 4.8 vorhanden, keine externe Abhaengigkeit).
    /// </summary>
    public static class ConfigStore
    {
        private const string FileName = "cs-config.json";
        // Alter Dateiname (kollidiert mit dem spieleigenen JsonConfig-Mechanismus) -> wird migriert + geloescht.
        private const string LegacyFileName = "config.json";

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(ModConfig));

        /// <summary>Voller Pfad zur config.json (Verzeichnis der Mod-DLL; Fallback auf den Standard-Mods-Pfad).</summary>
        /// <remarks>
        /// WICHTIG fuer die Variant-Isolation (Stable vs. Beta): Der Fallback-Ordner wird aus dem
        /// Assembly-NAMEN (<c>GetName().Name</c>) abgeleitet, NICHT aus der hartkodierten Konstante
        /// <see cref="CompanySupplier.ModName"/>. Grund: <c>manifest.json</c> setzt <c>non_locking_dll_load:true</c>,
        /// wodurch die DLL haeufig aus Bytes geladen wird und <c>Assembly.Location</c> LEER ist — dann greift
        /// der Fallback. Da <c>AssemblyName == manifest 'id'</c> (also "CompanySupplier" bzw. "CompanySupplierBeta"),
        /// landet die config.json so immer im EIGENEN Mod-Ordner und eine Beta kann den Stable nie ueberschreiben.
        /// </remarks>
        public static string ConfigPath
        {
            get
            {
                // Identitaet = Assembly-Name; ueberlebt leeres Assembly.Location (byte-geladene DLL).
                string modFolder = CompanySupplier.ModName;
                try
                {
                    string name = Assembly.GetExecutingAssembly().GetName().Name;
                    if (!string.IsNullOrEmpty(name)) modFolder = name;
                }
                catch { /* modFolder bleibt der ModName-Default */ }

                try
                {
                    string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (!string.IsNullOrEmpty(dir))
                        return Path.Combine(dir, FileName);
                }
                catch { /* faellt unten auf den AppData-Pfad zurueck */ }

                // Fallback (greift v.a. bei non_locking_dll_load): %APPDATA%\...\Mods\<assemblyName>\config.json
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "Captain of Industry", "Mods", modFolder, FileName);
            }
        }

        /// <summary>Laedt die Config; bei fehlender Datei oder Fehler eine frische <see cref="ModConfig"/>.
        /// Migriert vorher eine evtl. vorhandene alte <c>config.json</c> und raeumt sie weg.</summary>
        public static ModConfig Load()
        {
            try
            {
                string path = ConfigPath;
                MigrateLegacyIfNeeded(path);

                if (!File.Exists(path))
                {
                    Log.Info($"[{CompanySupplier.ModName}] Keine {FileName} — verwende Defaults ({path}).");
                    return new ModConfig();
                }
                using (var fs = File.OpenRead(path))
                {
                    var cfg = Serializer.ReadObject(fs) as ModConfig;
                    Log.Info($"[{CompanySupplier.ModName}] {FileName} geladen ({path}).");
                    return cfg ?? new ModConfig();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Config laden fehlgeschlagen ({ex.Message}) — Defaults.");
                return new ModConfig();
            }
        }

        /// <summary>Uebernimmt eine alte <c>config.json</c> (falls noch keine <c>cs-config.json</c> existiert)
        /// und LOESCHT sie danach — so verschwindet die spieleigene JsonConfig-Warnung dauerhaft und die
        /// gespeicherten Toggles/Presets/Favoriten bleiben erhalten. Best-effort: Fehler werden nur geloggt.</summary>
        private static void MigrateLegacyIfNeeded(string newPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(newPath);
                if (string.IsNullOrEmpty(dir)) return;
                string legacy = Path.Combine(dir, LegacyFileName);
                if (!File.Exists(legacy)) return;

                if (!File.Exists(newPath))
                {
                    using (var fs = File.OpenRead(legacy))
                    {
                        if (Serializer.ReadObject(fs) is ModConfig old)
                        {
                            Save(old);
                            Log.Info($"[{CompanySupplier.ModName}] Alte config.json nach {FileName} migriert.");
                        }
                    }
                }
                // Alte config.json entfernen -> das Spiel parst sie nicht mehr (keine Warnung mehr).
                File.Delete(legacy);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Legacy-Config-Migration: {ex.Message}");
            }
        }

        /// <summary>Speichert die Config (best-effort).</summary>
        public static void Save(ModConfig config)
        {
            if (config == null) return;
            try
            {
                string path = ConfigPath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Atomar schreiben: erst in eine Temp-Datei, dann ersetzen. Ein Absturz mitten im
                // Schreiben laesst so die alte config.json (inkl. aller Presets) intakt statt sie
                // mit einer halben Datei zu ueberschreiben.
                string tmp = path + ".tmp";
                using (var fs = File.Create(tmp))
                {
                    Serializer.WriteObject(fs, config);
                }
                if (File.Exists(path)) File.Replace(tmp, path, destinationBackupFileName: null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Config speichern fehlgeschlagen: {ex.Message}");
            }
        }
    }
}
