using System;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Economy;
using Mafi.Core.Maintenance;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

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
        public Cheats.TerrainCheats      Terrain      { get; private set; }
        public Cheats.WeatherCheats      Weather      { get; private set; }
        public Cheats.StorageToolCheats  StorageTool  { get; private set; }

        private CheatService(DependencyResolver resolver) => _resolver = resolver;

        /// <summary>Wird aus CompanySupplier.EarlyInit aufgerufen, sobald der DI-Container steht.</summary>
        public static void Create(DependencyResolver resolver)
        {
            Instance = new CheatService(resolver);
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
            Building     = new Cheats.BuildingCheats(_resolver);
            Population   = new Cheats.PopulationCheats(_resolver);
            Research     = new Cheats.ResearchCheats(_resolver);
            Generation   = new Cheats.GenerationCheats(_resolver);
            FleetVehicle = new Cheats.FleetVehicleCheats(_resolver);
            Terrain      = new Cheats.TerrainCheats(_resolver);
            Weather      = new Cheats.WeatherCheats(_resolver);
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
                if (disabled) CallNonPublic(_maintenance, "Cheat_RepairAllEntities");
                MaintenanceDisabled = disabled;
                Log.Info($"[{CompanySupplier.ModName}] Wartung deaktiviert = {disabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Wartung umschalten fehlgeschlagen: {ex.Message}");
            }
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
