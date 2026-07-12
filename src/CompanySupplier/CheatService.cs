using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Economy;
using Mafi.Core.Maintenance;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using CompanySupplier.Config;   // ModConfig, ConfigStore (Namespace auf Dateiebene -> nicht vom Typ CompanySupplier verdeckt)

namespace CompanySupplier
{
    /// <summary>
    /// Zentrale Cheat-Engine: holt die benoetigten Game-Manager aus dem DI-Container und stellt
    /// jeden Cheat als eine aufrufbare, UI-unabhaengige Methode bereit. Die spaetere Menue-UI (P5)
    /// bindet ihre Buttons/Schalter direkt an diese Methoden.
    ///
    /// Robustheits-Prinzip (Early-Access-API kann driften): jeder Manager wird mit TryResolve geholt
    /// und jeder Cheat in try/catch gekapselt — ein einzelner gebrochener Cheat darf weder den Mod
    /// noch das Spiel zum Absturz bringen, sondern wird nur ins Log geschrieben.
    /// </summary>
    public sealed class CheatService
    {
        public static CheatService Instance { get; private set; }

        private readonly DependencyResolver _resolver;

        /// <summary>Persistierte UI-/Komfort-Config (Toggle-Zustaende, Fenster, Presets). Liegt neben der
        /// Mod-DLL und greift NICHT in Spielstaende ein. Nie null (Defaults bei Ladefehler).</summary>
        public ModConfig Config { get; private set; } = new ModConfig();

        /// <summary>Speichert die Config (best-effort) — von der UI nach Aenderungen aufgerufen.</summary>
        public void SaveConfig() => ConfigStore.Save(Config);

        // Verifiziert gegen 0.8.5.0:
        private IAssetTransactionManager _assets;     // Produkte ins globale Lager legen
        private ProtosDb _protos;                     // Produktliste (ProductProto)
        private MaintenanceManager _maintenance;      // Wartung deaktivieren

        public bool MaintenanceDisabled { get; private set; }

        /// <summary>Produkt-/Prototyp-Datenbank des Spiels. Die UI nutzt diese, um spawnbare Produkte
        /// (z. B. fuer das Ressourcen-Dropdown) zu enumerieren — ohne eigenen Resolver-Zugriff.
        /// Kann null sein, falls die Aufloesung beim Laden fehlschlug (UI muss das tolerieren).</summary>
        public ProtosDb Protos => _protos;

        // ----------------------------------------------------------------------------------------
        // Cheat-Provider (eine Instanz je Themengruppe; je Provider holt sich seine Manager selbst
        // mit _resolver.TryResolve<T> und kapselt jeden Cheat in try/catch). Die UI (P5) bindet ihre
        // Buttons/Schalter direkt an diese Provider-Methoden.
        // ----------------------------------------------------------------------------------------
        public Cheats.BuildingCheats     Building     { get; private set; }
        public Cheats.PopulationCheats   Population   { get; private set; }
        public Cheats.ResearchCheats     Research     { get; private set; }
        public Cheats.GenerationCheats   Generation   { get; private set; }
        public Cheats.FleetVehicleCheats FleetVehicle { get; private set; }
        public Cheats.VehicleStatsCheats VehicleStats { get; private set; }
        public Cheats.TrainCheats        Train        { get; private set; }
        public Cheats.TerrainCheats      Terrain      { get; private set; }
        public Cheats.WeatherCheats      Weather      { get; private set; }
        public Cheats.StorageToolCheats  StorageTool  { get; private set; }
        public Cheats.SandboxCheats      Sandbox      { get; private set; }
        public Cheats.GameSpeedCheats    GameSpeed    { get; private set; }
        public Cheats.PollutionCheats    Pollution    { get; private set; }
        public Cheats.SourceSinkCheats   SourceSink   { get; private set; }
        public Cheats.WorldMapCheats     WorldMap     { get; private set; }
        public Cheats.ShipCheats         Ships        { get; private set; }
        public Cheats.BoostCheats        Boost        { get; private set; }
        public Cheats.StorageThroughputCheats StorageThroughput { get; private set; }
        public Cheats.StorageLabelCheats      StorageLabels     { get; private set; }
        public Cheats.GameplayCheats          Gameplay          { get; private set; }

        private CheatService(DependencyResolver resolver) => _resolver = resolver;

        /// <summary>Wird aus CompanySupplier.EarlyInit aufgerufen, sobald der DI-Container steht.</summary>
        public static void Create(DependencyResolver resolver)
        {
            Instance = new CheatService(resolver);
            Instance.Config = ConfigStore.Load();
            Instance.ResolveManagers();
            Log.Info($"[{CompanySupplier.ModName}] CheatService bereit.");
        }

        private void ResolveManagers()
        {
            _assets      = Resolve<IAssetTransactionManager>(nameof(IAssetTransactionManager));
            _protos      = Resolve<ProtosDb>(nameof(ProtosDb));
            _maintenance = Resolve<MaintenanceManager>(nameof(MaintenanceManager));

            // Themengruppen-Provider instanziieren. Jeder Provider-Ctor loest seine eigenen Manager
            // robust auf und haengt ggf. seine Kalender-Hooks ein — daher kein weiterer Wiring-Schritt.
            // Jeder Ctor ist einzeln gekapselt: wirft einer (z. B. TypeLoadException bei API-Drift),
            // wird nur DIESER Provider null (Toggle-Registry und Tabs null-checken bereits) statt dass
            // das komplette Mod-Laden abbricht.
            Building     = TryCreate(() => new Cheats.BuildingCheats(_resolver),     nameof(Cheats.BuildingCheats));
            Population   = TryCreate(() => new Cheats.PopulationCheats(_resolver),   nameof(Cheats.PopulationCheats));
            Research     = TryCreate(() => new Cheats.ResearchCheats(_resolver),     nameof(Cheats.ResearchCheats));
            Generation   = TryCreate(() => new Cheats.GenerationCheats(_resolver),   nameof(Cheats.GenerationCheats));
            FleetVehicle = TryCreate(() => new Cheats.FleetVehicleCheats(_resolver), nameof(Cheats.FleetVehicleCheats));
            VehicleStats = TryCreate(() => new Cheats.VehicleStatsCheats(_resolver), nameof(Cheats.VehicleStatsCheats));
            Train        = TryCreate(() => new Cheats.TrainCheats(_resolver),        nameof(Cheats.TrainCheats));
            Terrain      = TryCreate(() => new Cheats.TerrainCheats(_resolver),      nameof(Cheats.TerrainCheats));
            Weather      = TryCreate(() => new Cheats.WeatherCheats(_resolver),      nameof(Cheats.WeatherCheats));
            Sandbox      = TryCreate(() => new Cheats.SandboxCheats(_resolver),      nameof(Cheats.SandboxCheats));
            GameSpeed    = TryCreate(() => new Cheats.GameSpeedCheats(_resolver),    nameof(Cheats.GameSpeedCheats));
            Pollution    = TryCreate(() => new Cheats.PollutionCheats(_resolver),    nameof(Cheats.PollutionCheats));
            SourceSink   = TryCreate(() => new Cheats.SourceSinkCheats(_resolver),   nameof(Cheats.SourceSinkCheats));
            WorldMap     = TryCreate(() => new Cheats.WorldMapCheats(_resolver),     nameof(Cheats.WorldMapCheats));
            Ships        = TryCreate(() => new Cheats.ShipCheats(_resolver),         nameof(Cheats.ShipCheats));
            Boost        = TryCreate(() => new Cheats.BoostCheats(_resolver),        nameof(Cheats.BoostCheats));
            StorageThroughput = TryCreate(() => new Cheats.StorageThroughputCheats(_resolver), nameof(Cheats.StorageThroughputCheats));
            StorageLabels     = TryCreate(() => new Cheats.StorageLabelCheats(_resolver),      nameof(Cheats.StorageLabelCheats));
            Gameplay          = TryCreate(() => new Cheats.GameplayCheats(_resolver),          nameof(Cheats.GameplayCheats));
            // StorageToolCheats ist jetzt [GlobalDependency] (der StorageWandController bekommt es per DI
            // injiziert) -> hier DIESELBE DI-Instanz holen statt einer zweiten via new.
            StorageTool  = Resolve<Cheats.StorageToolCheats>(nameof(Cheats.StorageToolCheats));
        }

        // ----------------------------------------------------------------------------------------
        // Welt-Klick-Werkzeug "Lager-Zauberstab": der StorageWandController ist [GlobalDependency]
        // (vom DI-Container gebaut). Der Ressourcen-Tab aktiviert/deaktiviert ihn ueber diese Helfer,
        // damit der Tab parameterlos bleibt. Beide Abhaengigkeiten werden lazy + robust aufgeloest.
        // ----------------------------------------------------------------------------------------

        private Tools.StorageWandController _storageWand;
        private Mafi.Unity.IUnityInputMgr _inputMgr;

        /// <summary>Schaltet das Lager-Welt-Werkzeug im Fuell-Modus (KeepFull) an/aus.
        /// Beibehalten fuer bestehende Aufrufer; delegiert an die modus-faehige Ueberladung.</summary>
        public bool SetStorageWandActive(bool active)
            => SetStorageWandActive(active, Storage.StorageCheatMode.KeepFull);

        /// <summary>
        /// Schaltet das Lager-Welt-Werkzeug an/aus und setzt dabei den Ziel-Modus, den ein Lager-Klick
        /// toggelt (<paramref name="targetMode"/>: KeepFull = fuellen, KeepEmpty = leeren). Da nur EIN
        /// Controller existiert, genuegt es, vor dem (Re-)Aktivieren <c>TargetMode</c> zu setzen — ein
        /// erneutes <c>ActivateNewController</c> mit neuem Modus ist idempotent (kein Deactivate noetig).
        /// True bei Erfolg, false wenn DI-Teile fehlen.
        /// </summary>
        public bool SetStorageWandActive(bool active, Storage.StorageCheatMode targetMode)
        {
            try
            {
                if (_storageWand == null)
                    _resolver.TryResolve<Tools.StorageWandController>(out _storageWand);
                if (_inputMgr == null)
                    _resolver.TryResolve<Mafi.Unity.IUnityInputMgr>(out _inputMgr);

                if (_storageWand == null || _inputMgr == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] Lager-Werkzeug nicht verfuegbar (Controller/InputMgr nicht aufgeloest).");
                    return false;
                }

                // Modus IMMER vor dem (Re-)Aktivieren setzen — auch wenn schon aktiv, damit ein Wechsel
                // zwischen Fuell-/Leer-Werkzeug greift.
                _storageWand.TargetMode = targetMode;
                if (active) _inputMgr.ActivateNewController(_storageWand);
                else        _inputMgr.DeactivateController(_storageWand);
                // NACH dem (De-)Aktivieren synchronisieren: beim Werkzeug-Wechsel deaktiviert der
                // Input-Manager zuerst den alten Controller (dessen Deactivate feuert SyncAll im
                // Zwischenstand, in dem DIESES Werkzeug noch nicht aktiv ist). Dieser abschliessende
                // Sync sieht den endgueltigen Zustand -> der zuletzt geklickte Toggle stimmt.
                UI.CheatUiSync.SyncAll();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetStorageWandActive: {ex.Message}");
                return false;
            }
        }

        /// <summary>Der Ziel-Modus des aktiven Lager-Werkzeugs (fuer die UI-Spiegelung; None wenn inert).</summary>
        public Storage.StorageCheatMode StorageWandTargetMode
            => _storageWand?.TargetMode ?? Storage.StorageCheatMode.None;

        /// <summary>True, falls das Lager-Welt-Werkzeug gerade aktiv ist (fuer die UI-Spiegelung).</summary>
        public bool IsStorageWandActive => _storageWand != null && _storageWand.IsActive;

        // ----------------------------------------------------------------------------------------
        // Welt-Klick-Werkzeug "God-Tool": klickt Werften/Cargo-Depots/Fahrzeuge an und tankt sie voll.
        // Aktivierung analog zum Lager-Zauberstab ueber den IUnityInputMgr; lazy + robust aufgeloest.
        // ----------------------------------------------------------------------------------------

        private Tools.GodWandController _godWand;

        /// <summary>Schaltet das God-Werkzeug an/aus. True bei Erfolg, false wenn DI-Teile fehlen.</summary>
        public bool SetGodWandActive(bool active)
        {
            try
            {
                if (_godWand == null)
                    _resolver.TryResolve<Tools.GodWandController>(out _godWand);
                if (_inputMgr == null)
                    _resolver.TryResolve<Mafi.Unity.IUnityInputMgr>(out _inputMgr);

                if (_godWand == null || _inputMgr == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] God-Werkzeug nicht verfuegbar (Controller/InputMgr nicht aufgeloest).");
                    return false;
                }

                if (active) _inputMgr.ActivateNewController(_godWand);
                else        _inputMgr.DeactivateController(_godWand);
                // Abschliessender Sync nach dem Werkzeug-Wechsel (vgl. SetStorageWandActive): der
                // endgueltige Aktiv-Zustand gewinnt gegen den Zwischenstand aus dem Deactivate des
                // zuvor aktiven Werkzeugs.
                UI.CheatUiSync.SyncAll();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetGodWandActive: {ex.Message}");
                return false;
            }
        }

        /// <summary>True, falls das God-Werkzeug gerade aktiv ist (fuer die UI-Spiegelung).</summary>
        public bool IsGodWandActive => _godWand != null && _godWand.IsActive;

        /// <summary>Kapselt einen Provider-Ctor: wirft er, bleibt der Provider null und nur die
        /// betroffene Cheat-Gruppe ist inaktiv (Log statt Absturz beim Mod-/Save-Laden).</summary>
        private static T TryCreate<T>(Func<T> factory, string name) where T : class
        {
            try { return factory(); }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Provider {name} nicht verfuegbar: {ex.Message}");
                return null;
            }
        }

        private T Resolve<T>(string name) where T : class
        {
            try
            {
                if (_resolver.TryResolve<T>(out var dep) && dep != null)
                {
                    Log.Info($"[{CompanySupplier.ModName}]   + {name} aufgeloest.");
                    return dep;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}]   ! {name} Aufloesung warf: {ex.Message}");
                return null;
            }
            Log.Warning($"[{CompanySupplier.ModName}]   - {name} NICHT aufgeloest.");
            return null;
        }

        // ----------------------------------------------------------------------------------------
        // Cheat: alle Ressourcen geben  (Kern-Wunsch)
        // ----------------------------------------------------------------------------------------

        /// <summary>Legt <paramref name="quantity"/> Einheiten JEDES auf-LKW-ladbaren Produkts
        /// ins globale Lager (Werft). Erfuellt "alle Ressourcen geben" mit einem Aufruf.</summary>
        public void GiveAllResources(int quantity)
        {
            if (_assets == null || _protos == null) return;
            int count = 0;
            foreach (var proto in _protos.Filter<ProductProto>(p => p.CanBeLoadedOnTruck))
            {
                try
                {
                    _assets.StoreProduct(new ProductQuantity(proto, new Quantity(quantity)), CreateReason.Cheated);
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] Ressource {proto.Id} fehlgeschlagen: {ex.Message}");
                }
            }
            Log.Info($"[{CompanySupplier.ModName}] {quantity}x von {count} Produkten ins Lager gelegt.");
        }

        /// <summary>Legt <paramref name="quantity"/> eines einzelnen Produkts ins globale Lager.</summary>
        public void GiveResource(ProductProto proto, int quantity)
        {
            if (_assets == null || proto == null) return;
            try
            {
                _assets.StoreProduct(new ProductQuantity(proto, new Quantity(quantity)), CreateReason.Cheated);
                Log.Info($"[{CompanySupplier.ModName}] {quantity}x {proto.Id} ins Lager gelegt.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Ressource {proto.Id} fehlgeschlagen: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Cheat: Wartung deaktivieren  (Kern-Wunsch)
        // ----------------------------------------------------------------------------------------

        public void SetMaintenanceDisabled(bool disabled)
        {
            if (_maintenance == null) return;
            try
            {
                // 0.8.5.0: public Cheat-API statt frueherem Reflection-Hack auf m_maintenanceDisabled.
                _maintenance.Cheat_IgnoreMissingMaintenance(disabled);
                // Tracking SOFORT nach dem Umschalten setzen — der Repair-Sweep darunter darf den
                // gespiegelten Zustand nicht mehr kippen (sonst luegen UI + gespeicherter Zustand).
                MaintenanceDisabled = disabled;
                if (disabled)
                {
                    try { CallNonPublic(_maintenance, "Cheat_RepairAllEntities"); }
                    catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Reparatur-Sweep fehlgeschlagen (Wartung bleibt deaktiviert): {ex.Message}"); }
                }
                Log.Info($"[{CompanySupplier.ModName}] Wartung deaktiviert = {disabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Wartung umschalten fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>Fuellt sofort ALLE Wartungspuffer im Spiel auf (One-Shot; behebt Wartungs-Engpaesse ohne
        /// die Mechanik dauerhaft abzuschalten). Nutzt die public <c>MaintenanceManager.Cheat_FillAllMaintenanceBuffers</c>.</summary>
        public void FillAllMaintenance()
        {
            if (_maintenance == null) return;
            try
            {
                _maintenance.Cheat_FillAllMaintenanceBuffers();
                Log.Info($"[{CompanySupplier.ModName}] Alle Wartungspuffer aufgefuellt.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] FillAllMaintenance: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Zentrale Dauer-Cheat-Verwaltung (Basis fuer Panik-Aus, Auto-Restore, Presets)
        // ----------------------------------------------------------------------------------------

        /// <summary>Ein Dauer-Toggle: stabiler Schluessel + Setter + Leser. Null-sichere Lambdas, weil
        /// ein Provider bei Aufloesungsfehler null sein kann.</summary>
        private struct ToggleEntry
        {
            public string Key;
            public Action<bool> Apply;
            public Func<bool> Read;
        }

        /// <summary>Registry aller persistier-/wiederherstellbaren Dauer-Toggles. Jeder Eintrag bindet
        /// einen <see cref="ConfigKeys"/>-Schluessel an den passenden Provider-Setter/-Leser.</summary>
        private List<ToggleEntry> BuildToggleRegistry()
        {
            return new List<ToggleEntry>
            {
                new ToggleEntry { Key = ConfigKeys.SandboxNoPower,     Apply = v => Sandbox?.SetNoPowerNeeded(v),     Read = () => Sandbox?.NoPowerNeeded ?? false },
                new ToggleEntry { Key = ConfigKeys.SandboxNoWorkers,   Apply = v => Sandbox?.SetNoWorkersNeeded(v),   Read = () => Sandbox?.NoWorkersNeeded ?? false },
                new ToggleEntry { Key = ConfigKeys.SandboxNoComputing, Apply = v => Sandbox?.SetNoComputingNeeded(v), Read = () => Sandbox?.NoComputingNeeded ?? false },
                new ToggleEntry { Key = ConfigKeys.SandboxNoUnity,     Apply = v => Sandbox?.SetNoUnityNeeded(v),     Read = () => Sandbox?.NoUnityNeeded ?? false },
                new ToggleEntry { Key = ConfigKeys.SandboxNoFood,      Apply = v => Sandbox?.SetNoFoodNeeded(v),      Read = () => Sandbox?.NoFoodNeeded ?? false },
                new ToggleEntry { Key = ConfigKeys.InstaBuild,         Apply = v => Building?.SetInstaBuild(v),        Read = () => Building?.InstaBuildEnabled ?? false },
                new ToggleEntry { Key = ConfigKeys.NoFuel,             Apply = v => FleetVehicle?.SetFuelConsumptionDisabled(v), Read = () => FleetVehicle?.FuelConsumptionDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.NoMaintenance,      Apply = SetMaintenanceDisabled,                Read = () => MaintenanceDisabled },
                new ToggleEntry { Key = ConfigKeys.DiseasesDisabled,   Apply = v => Population?.SetDiseasesDisabled(v),Read = () => Population?.DiseasesDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.MaxHappiness,       Apply = v => Population?.SetMaxConsumptionHappiness(v), Read = () => Population?.MaxConsumptionHappiness ?? false },
                new ToggleEntry { Key = ConfigKeys.KeepUnityFull,      Apply = v => Population?.SetKeepUnityFull(v),    Read = () => Population?.KeepUnityFull ?? false },
                new ToggleEntry { Key = ConfigKeys.HousingNoWaste,     Apply = v => Population?.SetNoMunicipalWaste(v), Read = () => Population?.NoMunicipalWaste ?? false },
                new ToggleEntry { Key = ConfigKeys.HousingNoBiowaste,  Apply = v => Population?.SetNoBiowaste(v),       Read = () => Population?.NoBiowaste ?? false },
                new ToggleEntry { Key = ConfigKeys.ResIgnoreItemReq,   Apply = v => Research?.SetIgnoreItemRequirements(v),   Read = () => Research?.IgnoreItemRequirements ?? false },
                new ToggleEntry { Key = ConfigKeys.ResIgnoreParentReq, Apply = v => Research?.SetIgnoreParentRequirements(v), Read = () => Research?.IgnoreParentRequirements ?? false },

                new ToggleEntry { Key = ConfigKeys.PollutionAir,       Apply = v => Pollution?.SetAirDisabled(v),      Read = () => Pollution?.AirDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.PollutionWater,     Apply = v => Pollution?.SetWaterDisabled(v),    Read = () => Pollution?.WaterDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.PollutionLandfill,  Apply = v => Pollution?.SetLandfillDisabled(v), Read = () => Pollution?.LandfillDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.PollutionVehicles,  Apply = v => Pollution?.SetVehiclesDisabled(v), Read = () => Pollution?.VehiclesDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.PollutionShips,     Apply = v => Pollution?.SetShipsDisabled(v),    Read = () => Pollution?.ShipsDisabled ?? false },
                new ToggleEntry { Key = ConfigKeys.PollutionTrains,    Apply = v => Pollution?.SetTrainsDisabled(v),   Read = () => Pollution?.TrainsDisabled ?? false },

                new ToggleEntry { Key = ConfigKeys.WorldUnlimitedMines,Apply = v => WorldMap?.SetUnlimitedMines(v),    Read = () => WorldMap?.UnlimitedMines ?? false },
                new ToggleEntry { Key = ConfigKeys.WorldMinesNoUnity,  Apply = v => WorldMap?.SetMinesNoUnity(v),      Read = () => WorldMap?.MinesNoUnity ?? false },
                new ToggleEntry { Key = ConfigKeys.WorldMinesEffMax,   Apply = v => WorldMap?.SetMinesEfficiencyMax(v),Read = () => WorldMap?.MinesEfficiencyMax ?? false },
                new ToggleEntry { Key = ConfigKeys.WorldTradeBoost,    Apply = v => WorldMap?.SetTradeBoost(v),        Read = () => WorldMap?.TradeBoosted ?? false },

                new ToggleEntry { Key = ConfigKeys.ShipsNoFuel,        Apply = v => Ships?.SetShipsFuelDisabled(v),    Read = () => Ships?.ShipsFuelDisabled ?? false },

                new ToggleEntry { Key = ConfigKeys.ProdMining,         Apply = v => Boost?.SetMiningBoost(v),          Read = () => Boost?.MiningBoost ?? false },
                new ToggleEntry { Key = ConfigKeys.ProdFarm,           Apply = v => Boost?.SetFarmBoost(v),            Read = () => Boost?.FarmBoost ?? false },
                new ToggleEntry { Key = ConfigKeys.ProdSolar,          Apply = v => Boost?.SetSolarBoost(v),           Read = () => Boost?.SolarBoost ?? false },
                new ToggleEntry { Key = ConfigKeys.ProdForceRun,       Apply = v => Boost?.SetForceRunMachines(v),     Read = () => Boost?.ForceRunMachines ?? false },
                new ToggleEntry { Key = ConfigKeys.ProdUnlimitedWater, Apply = v => Boost?.SetUnlimitedWater(v),       Read = () => Boost?.UnlimitedWater ?? false },
                new ToggleEntry { Key = ConfigKeys.ProdNoOilDrain,     Apply = v => Boost?.SetNoOilDrain(v),           Read = () => Boost?.NoOilDrain ?? false },

                // Pipe-Cheat (Harmony): statischer Schalter, den der InitPathFinding-Patch pro Aufruf liest.
                new ToggleEntry { Key = ConfigKeys.PipeSlopes,         Apply = v => HarmonyIntegration.PipeCheats.BuildAlongSlopes = v, Read = () => HarmonyIntegration.PipeCheats.BuildAlongSlopes },

                // Gameplay-Hebel (globale PropertyId-Toggles).
                new ToggleEntry { Key = ConfigKeys.TrainsNoFuel,       Apply = v => Gameplay?.SetTrainsNoFuel(v),      Read = () => Gameplay?.TrainsNoFuel ?? false },
                new ToggleEntry { Key = ConfigKeys.FreeBuild,          Apply = v => Gameplay?.SetFreeBuild(v),         Read = () => Gameplay?.FreeBuild ?? false },
                new ToggleEntry { Key = ConfigKeys.MachineLowPower,    Apply = v => Gameplay?.SetMachineFullOnLowPower(v),     Read = () => Gameplay?.MachineFullOnLowPower ?? false },
                new ToggleEntry { Key = ConfigKeys.MachineLowComputing,Apply = v => Gameplay?.SetMachineFullOnLowComputing(v), Read = () => Gameplay?.MachineFullOnLowComputing ?? false },
                new ToggleEntry { Key = ConfigKeys.NoConsumption,      Apply = v => Gameplay?.SetNoSettlementConsumption(v),   Read = () => Gameplay?.NoSettlementConsumption ?? false },
                new ToggleEntry { Key = ConfigKeys.HousingCapacity,    Apply = v => Gameplay?.SetHousingCapacityBoost(v),     Read = () => Gameplay?.HousingCapacityBoost ?? false },

                // Gameplay-Hebel Welle 2.
                new ToggleEntry { Key = ConfigKeys.NoMaintConsume,     Apply = v => Gameplay?.SetNoMaintenanceConsumption(v), Read = () => Gameplay?.NoMaintenanceConsumption ?? false },
                new ToggleEntry { Key = ConfigKeys.NoFarmWater,        Apply = v => Gameplay?.SetNoFarmWater(v),        Read = () => Gameplay?.NoFarmWater ?? false },
                new ToggleEntry { Key = ConfigKeys.UnityProduction,    Apply = v => Gameplay?.SetUnityProductionBoost(v), Read = () => Gameplay?.UnityProductionBoost ?? false },
                new ToggleEntry { Key = ConfigKeys.TrainPower,         Apply = v => Gameplay?.SetTrainPowerBoost(v),    Read = () => Gameplay?.TrainPowerBoost ?? false },
                new ToggleEntry { Key = ConfigKeys.TrainSlopes,        Apply = v => Gameplay?.SetTrainsIgnoreSlopes(v), Read = () => Gameplay?.TrainsIgnoreSlopes ?? false },
                new ToggleEntry { Key = ConfigKeys.LogisticsPower,     Apply = v => Gameplay?.SetLogisticsIgnorePower(v), Read = () => Gameplay?.LogisticsIgnorePower ?? false },
                new ToggleEntry { Key = ConfigKeys.RecyclingFull,      Apply = v => Gameplay?.SetRecyclingFull(v),      Read = () => Gameplay?.RecyclingFull ?? false },
                new ToggleEntry { Key = ConfigKeys.TreeGrowth,         Apply = v => Gameplay?.SetTreeGrowthBoost(v),    Read = () => Gameplay?.TreeGrowthBoost ?? false },
                new ToggleEntry { Key = ConfigKeys.RocketCapacity,     Apply = v => Gameplay?.SetRocketCapacityBoost(v), Read = () => Gameplay?.RocketCapacityBoost ?? false },

                new ToggleEntry { Key = ConfigKeys.SourceSinkEnabled,  Apply = v => SourceSink?.SetEnabled(v),         Read = () => SourceSink?.Enabled ?? false },

                // Nur bei tatsaechlicher Aenderung anwenden: SetUncapped(false) setzt intern die
                // Geschwindigkeit auf 1x zurueck — ein blindes Re-Apply von "aus" wuerde sonst bei
                // jedem Preset-Laden/Auto-Restore die laufende Spielgeschwindigkeit kippen.
                new ToggleEntry { Key = ConfigKeys.GameSpeedUncapped,  Apply = v => { if (GameSpeed != null && GameSpeed.Uncapped != v) GameSpeed.SetUncapped(v); }, Read = () => GameSpeed?.Uncapped ?? false },
            };
        }

        /// <summary>Liest den aktuellen Zustand ALLER Dauer-Toggles in eine Liste (fuer Speichern/Presets).</summary>
        public List<ToggleState> CaptureCurrentState()
        {
            var result = new List<ToggleState>();
            foreach (var e in BuildToggleRegistry())
            {
                bool val = false;
                try { val = e.Read(); } catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] CaptureCurrentState({e.Key}): {ex.Message}"); }
                result.Add(new ToggleState { Key = e.Key, Value = val });
            }
            return result;
        }

        /// <summary>Wendet eine Liste gespeicherter Toggle-Zustaende an (Auto-Restore / Preset laden).</summary>
        public void ApplyState(List<ToggleState> toggles)
        {
            if (toggles == null) return;
            var registry = BuildToggleRegistry();
            foreach (var ts in toggles)
            {
                if (ts == null) continue;
                foreach (var e in registry)
                {
                    if (e.Key != ts.Key) continue;
                    try { e.Apply?.Invoke(ts.Value); }
                    catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ApplyState({ts.Key}): {ex.Message}"); }
                    break;
                }
            }
            Log.Info($"[{CompanySupplier.ModName}] {toggles.Count} Toggle-Zustaende angewendet.");
            // Menue-Toggles nachziehen: die Tabs bauen ihren Content nur einmal (DI-Ctor), ohne diesen
            // Sync wuerden sie nach Auto-Restore/Preset-Laden den alten Zustand anzeigen.
            UI.CheatUiSync.SyncAll();
        }

        /// <summary>Panik-Aus: schaltet ALLE Dauer-Cheats ab (inkl. Dauer-Erzeugung) und setzt die
        /// Geschwindigkeit zurueck.</summary>
        public void DisableAllContinuousCheats()
        {
            foreach (var e in BuildToggleRegistry())
            {
                try { e.Apply?.Invoke(false); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Panik-Aus({e.Key}): {ex.Message}"); }
            }
            // Dauer-Erzeugung (Werte statt Toggles, daher nicht in der Registry) ebenfalls stoppen —
            // "alle Dauer-Cheats" muss auch Gratis-Strom/-Computing/-Unity umfassen.
            try
            {
                Generation?.SetFreeElectricityPerTick(0);
                Generation?.SetFreeComputingPerTick(0);
                Generation?.SetUnityPerMonth(0);
                Generation?.SetFakePowerConsumption(0);
                Generation?.SetFakeComputingConsumption(0);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Panik-Aus(Erzeugung): {ex.Message}"); }
            GameSpeed?.SetSpeed(1);
            Log.Info($"[{CompanySupplier.ModName}] Panik-Aus: alle Dauer-Cheats deaktiviert.");
            UI.CheatUiSync.SyncAll();
        }

        /// <summary>Erfasst den aktuellen Zustand in die Config und speichert sie.</summary>
        public void SaveCurrentStateToConfig()
        {
            Config.Toggles = CaptureCurrentState();
            SaveConfig();
        }

        // -- Preset-Slots (feste Slots 1..N, kein Text-Input noetig) -------------------------------

        private static string PresetName(int slot) => "Slot " + slot;

        /// <summary>Speichert den aktuellen Dauer-Cheat-Zustand in den Preset-Slot <paramref name="slot"/>.</summary>
        public void SavePreset(int slot)
        {
            if (Config.Presets == null) Config.Presets = new List<CheatPreset>();
            string name = PresetName(slot);
            CheatPreset preset = null;
            foreach (var p in Config.Presets) if (p != null && p.Name == name) { preset = p; break; }
            if (preset == null) { preset = new CheatPreset { Name = name }; Config.Presets.Add(preset); }
            preset.Toggles = CaptureCurrentState();
            SaveConfig();
            Log.Info($"[{CompanySupplier.ModName}] Preset '{name}' gespeichert.");
        }

        /// <summary>Laedt und wendet den Preset-Slot <paramref name="slot"/> an. False, wenn der Slot leer ist.</summary>
        public bool LoadPreset(int slot)
        {
            string name = PresetName(slot);
            if (Config.Presets != null)
                foreach (var p in Config.Presets)
                    if (p != null && p.Name == name) { ApplyState(p.Toggles); return true; }
            return false;
        }

        /// <summary>True, wenn der Preset-Slot belegt ist (fuer die UI-Beschriftung).</summary>
        public bool HasPreset(int slot)
        {
            string name = PresetName(slot);
            if (Config.Presets != null)
                foreach (var p in Config.Presets)
                    if (p != null && p.Name == name) return true;
            return false;
        }

        // ----------------------------------------------------------------------------------------
        // Hilfen
        // ----------------------------------------------------------------------------------------

        /// <summary>Ruft eine internal/private Instanzmethode per Reflection auf (fuer interne Cheat_-APIs).
        /// Bei fehlendem Member (API-Drift) wird geloggt statt still verschluckt.</summary>
        private static void CallNonPublic(object target, string method, params object[] args)
        {
            if (target == null) return;
            var mi = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (mi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Methode {method} auf {target.GetType().Name} nicht gefunden (API-Drift?).");
                return;
            }
            mi.Invoke(target, args.Length == 0 ? null : args);
        }
    }
}
