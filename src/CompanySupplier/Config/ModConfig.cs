using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CompanySupplier.Config
{
    /// <summary>
    /// Persistierter Mod-Zustand (UI-/Komfort-Schicht) — bewusst GETRENNT vom Spielstand: die Cheats
    /// selbst bleiben <c>NonSaveable</c>, diese Datei haelt nur, was ueber Spielsitzungen hinweg bequem
    /// erhalten bleiben soll (zuletzt aktive Dauer-Toggles fuer Auto-Restore, zuletzt geoeffneter Reiter,
    /// Presets).
    ///
    /// Serialisiert ueber <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/>
    /// (in .NET 4.8 vorhanden; keine externe Abhaengigkeit). Statt eines Dictionary (das der
    /// DataContract-Serializer umstaendlich abbildet) werden Schluessel/Wert-Paare als
    /// <see cref="ToggleState"/>-Liste gehalten — robust und vorhersehbar.
    /// </summary>
    [DataContract(Name = "ModConfig")]
    public sealed class ModConfig
    {
        /// <summary>Zuletzt bekannte Zustaende der Dauer-Toggles (Schluessel siehe <see cref="ConfigKeys"/>).</summary>
        [DataMember(Name = "toggles", Order = 0)]
        public List<ToggleState> Toggles { get; set; } = new List<ToggleState>();

        /// <summary>Beim Laden eines Spielstands die zuletzt aktiven Dauer-Toggles automatisch reaktivieren.</summary>
        [DataMember(Name = "autoRestore", Order = 1)]
        public bool AutoRestore { get; set; } = true;

        /// <summary>Zuletzt geoeffneter Reiter-Index (Fenster-Komfort; bleibt ueber Spielstarts erhalten).</summary>
        [DataMember(Name = "lastTabIndex", Order = 2)]
        public int LastTabIndex { get; set; }

        /// <summary>Benannte Cheat-Presets (Bundles von Dauer-Toggle-Zustaenden).</summary>
        [DataMember(Name = "presets", Order = 3)]
        public List<CheatPreset> Presets { get; set; } = new List<CheatPreset>();

        // ----------------------------------------------------------------------------------------
        // Komfort-Helfer fuer den Toggle-Zustand
        // ----------------------------------------------------------------------------------------

        /// <summary>Liefert den gespeicherten Wert eines Toggles oder <paramref name="fallback"/>.</summary>
        public bool GetToggle(string key, bool fallback = false)
        {
            if (Toggles == null) return fallback;
            foreach (var t in Toggles)
                if (t != null && t.Key == key) return t.Value;
            return fallback;
        }

        /// <summary>Setzt den Wert eines Toggles (legt den Eintrag bei Bedarf an).</summary>
        public void SetToggle(string key, bool value)
        {
            if (Toggles == null) Toggles = new List<ToggleState>();
            foreach (var t in Toggles)
            {
                if (t != null && t.Key == key) { t.Value = value; return; }
            }
            Toggles.Add(new ToggleState { Key = key, Value = value });
        }
    }

    /// <summary>Ein einzelner persistierter Toggle-Zustand (Schluessel -> bool).</summary>
    [DataContract(Name = "Toggle")]
    public sealed class ToggleState
    {
        [DataMember(Name = "key", Order = 0)]
        public string Key { get; set; }

        [DataMember(Name = "value", Order = 1)]
        public bool Value { get; set; }
    }

    /// <summary>Ein benanntes Preset: ein Name + die zugehoerigen Toggle-Zustaende.</summary>
    [DataContract(Name = "Preset")]
    public sealed class CheatPreset
    {
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; }

        [DataMember(Name = "toggles", Order = 1)]
        public List<ToggleState> Toggles { get; set; } = new List<ToggleState>();
    }

    /// <summary>Stabile Schluessel fuer die persistierten Dauer-Toggles (ein Ort, damit UI + Auto-Restore
    /// dieselben Strings nutzen).</summary>
    public static class ConfigKeys
    {
        public const string SandboxNoPower      = "sandbox.noPower";
        public const string SandboxNoWorkers    = "sandbox.noWorkers";
        public const string SandboxNoComputing  = "sandbox.noComputing";
        public const string SandboxNoUnity      = "sandbox.noUnity";
        public const string SandboxNoFood       = "sandbox.noFood";
        public const string InstaBuild          = "build.instaBuild";
        public const string NoFuel              = "vehicle.noFuel";
        public const string NoMaintenance       = "build.noMaintenance";
        public const string DiseasesDisabled    = "pop.noDiseases";
        public const string MaxHappiness        = "pop.maxHappiness";

        public const string PollutionAir        = "pollution.air";
        public const string PollutionWater      = "pollution.water";
        public const string PollutionLandfill   = "pollution.landfill";
        public const string PollutionVehicles   = "pollution.vehicles";
        public const string PollutionShips      = "pollution.ships";
        public const string PollutionTrains     = "pollution.trains";

        public const string WorldUnlimitedMines = "world.unlimitedMines";
        public const string WorldMinesNoUnity   = "world.minesNoUnity";
        public const string WorldMinesEffMax    = "world.minesEffMax";
        public const string WorldTradeBoost     = "world.tradeBoost";

        public const string SourceSinkEnabled   = "sandbox.sourceSink";
    }
}
