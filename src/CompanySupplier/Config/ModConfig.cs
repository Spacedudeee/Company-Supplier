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

        /// <summary>Als Favorit markierte Produkte (stabile <c>ProductProto.Id.Value</c>-Strings) fuer den
        /// Ressourcen-Tab. Reine UI-/Komfort-Liste, unabhaengig vom Spielstand; unbekannte Ids werden beim
        /// Anzeigen still uebersprungen.</summary>
        [DataMember(Name = "favorites", Order = 4)]
        public List<string> FavoriteProductIds { get; set; } = new List<string>();

        /// <summary>
        /// WICHTIG: <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/> erzeugt
        /// Instanzen OHNE Konstruktor/Initializer (GetUninitializedObject). Fehlt ein Member im JSON,
        /// bliebe er sonst auf dem CLR-Default (false/null) statt auf dem deklarierten Default —
        /// z. B. wuerde <see cref="AutoRestore"/> still zu false. Dieser Callback stellt die Defaults
        /// VOR der Member-Zuweisung wieder her (vorhandene JSON-Werte ueberschreiben sie danach).
        /// </summary>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            Toggles = new List<ToggleState>();
            AutoRestore = true;
            Presets = new List<CheatPreset>();
            FavoriteProductIds = new List<string>();
        }

        // ----------------------------------------------------------------------------------------
        // Komfort-Helfer fuer die Produkt-Favoriten (Ressourcen-Tab)
        // ----------------------------------------------------------------------------------------

        /// <summary>True, wenn die Produkt-Id als Favorit markiert ist.</summary>
        public bool IsFavorite(string productId)
            => productId != null && FavoriteProductIds != null && FavoriteProductIds.Contains(productId);

        /// <summary>Schaltet den Favoriten-Status einer Produkt-Id um. Liefert den neuen Zustand.</summary>
        public bool ToggleFavorite(string productId)
        {
            if (productId == null) return false;
            if (FavoriteProductIds == null) FavoriteProductIds = new List<string>();
            if (FavoriteProductIds.Contains(productId)) { FavoriteProductIds.Remove(productId); return false; }
            FavoriteProductIds.Add(productId);
            return true;
        }

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

        // Initializer laufen beim Deserialisieren nicht (s. ModConfig.OnDeserializing) — Default hier
        // ebenfalls per Callback absichern, damit Toggles nie null ist.
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            Toggles = new List<ToggleState>();
        }
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
        public const string KeepUnityFull       = "pop.keepUnityFull";
        public const string HousingNoWaste      = "pop.noWaste";
        public const string HousingNoBiowaste   = "pop.noBiowaste";
        public const string ResIgnoreItemReq    = "research.ignoreItemReq";
        public const string ResIgnoreParentReq  = "research.ignoreParentReq";

        public const string PollutionAir        = "pollution.air";
        public const string PollutionWater      = "pollution.water";
        public const string PollutionLandfill   = "pollution.landfill";
        public const string PollutionVehicles   = "pollution.vehicles";
        public const string PollutionShips      = "pollution.ships";
        public const string PollutionTrains     = "pollution.trains";
        public const string ShipsNoFuel         = "ship.noFuel";
        public const string ProdMining          = "prod.mining";
        public const string ProdFarm            = "prod.farm";
        public const string ProdSolar           = "prod.solar";
        public const string ProdForceRun        = "prod.forceRun";
        public const string ProdUnlimitedWater  = "prod.unlimitedWater";
        public const string ProdNoOilDrain      = "prod.noOilDrain";
        public const string PipeSlopes          = "pipe.slopes";

        public const string TrainsNoFuel        = "gameplay.trainsNoFuel";
        public const string FreeBuild           = "gameplay.freeBuild";
        public const string MachineLowPower     = "gameplay.machineLowPower";
        public const string MachineLowComputing = "gameplay.machineLowComputing";
        public const string NoConsumption       = "gameplay.noConsumption";
        public const string HousingCapacity     = "gameplay.housingCapacity";

        public const string WorldUnlimitedMines = "world.unlimitedMines";
        public const string WorldMinesNoUnity   = "world.minesNoUnity";
        public const string WorldMinesEffMax    = "world.minesEffMax";
        public const string WorldTradeBoost     = "world.tradeBoost";

        public const string SourceSinkEnabled   = "sandbox.sourceSink";

        public const string GameSpeedUncapped   = "speed.uncapped";
    }
}
