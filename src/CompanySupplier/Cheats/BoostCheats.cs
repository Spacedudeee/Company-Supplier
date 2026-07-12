using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core;                       // IdsCore.PropertyIds, PropertyId<T>
using Mafi.Core.PropertiesDb;          // IPropertiesDb, IProperty<T>, PropertyModifiers, PropertyModifier<T>
using Mafi.Core.Products;              // VirtualResourceProductProto (Erdoel-Vorkommen)
using Mafi.Core.Prototypes;            // ProtosDb
using Mafi.Core.Simulation;            // ICalendar (Tages-Hook fuer Erdoel-Refill)
using Mafi.Core.Terrain;               // VirtualResourceManager

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Produktion & Reserven": Dauer-Boosts ueber globale PropertyId-Modifier — exakt das
    /// Muster von <see cref="WorldMapCheats"/>/<see cref="PollutionCheats"/> (additiver Modifier je eigenem
    /// Owner, Reset entfernt nur unseren Beitrag, Status live aus der Modifier-Praesenz gelesen).
    ///
    /// Hebel (alle gegen 0.8.5.0 verifiziert, IdsCore.PropertyIds):
    ///  - Foerderleistung  : MiningMultiplier        (+900% -> 10x)
    ///  - Farm-Ertrag      : FarmYieldMultiplier      (+900% -> 10x)
    ///  - Solar-Ertrag     : SolarPowerMultiplier     (+900% -> 10x)
    ///  - Zwangslauf        : ForceRunAllMachinesEnabled (bool true)
    ///  - Unerschoepfliches Grundwasser: GroundWaterPumpSpeedWhenDepleted (+100%) UND
    ///    GroundWaterReplenishWhenLow (+900%) unter EINEM Owner -> ein Toggle, ein Reset.
    ///  - Kein Erdoel-Verbrauch: es gibt KEINE Property dafuer — die Erdoel-Vorkommen (VirtualResource)
    ///    werden per Tages-Hook wieder auf Kapazitaet aufgefuellt (Muster wie TerrainCheats.FillGroundCrude).
    ///
    /// Robustheit: alle Manager via TryResolve; jeder Zugriff null-guarded + try/catch + Log.Warning.
    /// Implementiert <see cref="IEventOwner"/> fuer das (NonSaveable) NewDay-Abo des Erdoel-Refills.
    /// </summary>
    public sealed class BoostCheats : IEventOwner
    {
        // Distinkter Owner je Hebel -> Reset kollidiert nie mit fremden Modifiern oder anderen CS-Domains.
        private const string OwnerMining   = "CompanySupplier.Boost.Mining";
        private const string OwnerFarm     = "CompanySupplier.Boost.Farm";
        private const string OwnerSolar    = "CompanySupplier.Boost.Solar";
        private const string OwnerForceRun = "CompanySupplier.Boost.ForceRun";
        private const string OwnerWater    = "CompanySupplier.Boost.Water";

        private const string OwnerOil      = "CompanySupplier.Boost.Oil";

        private readonly DependencyResolver _resolver;
        private IPropertiesDb _db;
        private VirtualResourceManager _virtualResources;   // Erdoel-Vorkommen (Refill)
        private ProtosDb _protos;                           // Erdoel-Proto-Suche
        private ICalendar _calendar;                        // Tages-Hook fuer den Erdoel-Refill

        public BoostCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _db);
            _resolver.TryResolve<VirtualResourceManager>(out _virtualResources);
            _resolver.TryResolve<ProtosDb>(out _protos);

            if (_resolver.TryResolve<ICalendar>(out _calendar) && _calendar != null)
            {
                try { _calendar.NewDay.AddNonSaveable(this, OnNewDay); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] BoostCheats NewDay-Abo fehlgeschlagen: {ex.Message}"); }
            }
        }

        /// <summary><see cref="IEventOwner"/>: der Provider lebt so lange wie der Mod, wird nie zerstoert.</summary>
        public bool IsDestroyed => false;

        // ---- oeffentliche Hebel -------------------------------------------------------------------

        public bool MiningBoost           => HasPercent(IdsCore.PropertyIds.MiningMultiplier,  OwnerMining);
        public void SetMiningBoost(bool v)  => SetPercent(IdsCore.PropertyIds.MiningMultiplier, OwnerMining, 900, v, "Foerderleistung");

        public bool FarmBoost             => HasPercent(IdsCore.PropertyIds.FarmYieldMultiplier, OwnerFarm);
        public void SetFarmBoost(bool v)    => SetPercent(IdsCore.PropertyIds.FarmYieldMultiplier, OwnerFarm, 900, v, "Farm-Ertrag");

        public bool SolarBoost            => HasPercent(IdsCore.PropertyIds.SolarPowerMultiplier, OwnerSolar);
        public void SetSolarBoost(bool v)   => SetPercent(IdsCore.PropertyIds.SolarPowerMultiplier, OwnerSolar, 900, v, "Solar-Ertrag");

        public bool ForceRunMachines      => HasBool(IdsCore.PropertyIds.ForceRunAllMachinesEnabled, OwnerForceRun);
        public void SetForceRunMachines(bool v) => SetBool(IdsCore.PropertyIds.ForceRunAllMachinesEnabled, OwnerForceRun, true, v, "Alle Maschinen zwangslaufen");

        /// <summary>Unerschoepfliches Grundwasser: pumpt auch im erschoepften Zustand voll UND fuellt sich stark
        /// wieder auf. Beide Modifier unter einem Owner -> ein Toggle deckt/entfernt beide.</summary>
        public bool UnlimitedWater => HasPercent(IdsCore.PropertyIds.GroundWaterPumpSpeedWhenDepleted, OwnerWater);
        public void SetUnlimitedWater(bool v)
        {
            SetPercent(IdsCore.PropertyIds.GroundWaterPumpSpeedWhenDepleted, OwnerWater, 100, v, "Grundwasser-Pumpe (erschoepft)");
            SetPercent(IdsCore.PropertyIds.GroundWaterReplenishWhenLow,      OwnerWater, 900, v, "Grundwasser-Auffuellung");
        }

        // ---- Kein Erdoel-Verbrauch (Tages-Refill der Erdoel-Vorkommen) ---------------------------

        /// <summary>true = die Erdoel-Vorkommen werden taeglich wieder auf Kapazitaet aufgefuellt (kein
        /// dauerhafter Freeze, sondern ein Refill pro Tag — der Verbrauch waehrend des Tages bleibt sichtbar).</summary>
        public bool NoOilDrain { get; private set; }

        /// <summary>Schaltet "Kein Erdoel-Verbrauch" ein/aus. Bei "an" wird sofort einmal aufgefuellt; der
        /// Tages-Hook (OnNewDay) haelt die Vorkommen danach voll.</summary>
        public void SetNoOilDrain(bool enabled)
        {
            NoOilDrain = enabled;
            if (enabled) RefillCrudeOil();
            Log.Info($"[{CompanySupplier.ModName}] Kein Erdoel-Verbrauch = {enabled}.");
        }

        /// <summary>Fuellt alle Erdoel-Vorkommen bis zur Kapazitaet auf. Findet das Erdoel-<c>VirtualResourceProductProto</c>
        /// ueber Id-Substring (drift-resistent, wie <c>TerrainCheats.FillGroundCrude</c>).</summary>
        private void RefillCrudeOil()
        {
            if (_virtualResources == null || _protos == null) return;
            try
            {
                var protos = _protos.Filter<VirtualResourceProductProto>(_ => true).ToList();
                VirtualResourceProductProto oil = null;
                foreach (var needle in new[] { "crude", "oil" })
                {
                    oil = protos.FirstOrDefault(p => p.Id.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (oil != null) break;
                }
                if (oil == null) { Log.Warning($"[{CompanySupplier.ModName}] Kein Erdoel-Verbrauch: kein Erdoel-Vorkommen gefunden."); return; }

                int count = 0;
                foreach (var res in _virtualResources.GetAllResourcesFor(oil))
                {
                    res.AddAsMuchAs(res.Capacity);
                    count++;
                }
                Log.Info($"[{CompanySupplier.ModName}] Erdoel-Vorkommen aufgefuellt ({oil.Id}): {count} Stueck.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] RefillCrudeOil: {ex.Message}"); }
        }

        private void OnNewDay()
        {
            if (NoOilDrain) RefillCrudeOil();
        }

        // ---- generische Helfer (ein IProperty<T>-Cache pro Aufruf; robust) ------------------------

        private bool HasPercent(PropertyId<Percent> id, string owner)
        {
            try
            {
                IProperty<Percent> prop = _db?.GetProperty(id);
                return prop != null && prop.TryGetModifier(owner, out PropertyModifier<Percent> _);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] HasPercent({id}): {ex.Message}"); return false; }
        }

        private void SetPercent(PropertyId<Percent> id, string owner, int deltaPercent, bool on, string label)
        {
            if (_db == null) return;
            try
            {
                IProperty<Percent> prop = _db.GetProperty(id);
                if (prop == null) { Log.Warning($"[{CompanySupplier.ModName}] {label}: Property nicht gefunden."); return; }
                if (on) prop.AddOrSetModifier(owner, Percent.FromPercentVal(deltaPercent), PropertyModifiers.NO_GROUP);
                else    prop.TryRemoveModifier(owner);
                Log.Info($"[{CompanySupplier.ModName}] {label} = {on} ({deltaPercent:+0;-0}%).");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] {label} umschalten: {ex.Message}"); }
        }

        private bool HasBool(PropertyId<bool> id, string owner)
        {
            try
            {
                IProperty<bool> prop = _db?.GetProperty(id);
                return prop != null && prop.TryGetModifier(owner, out PropertyModifier<bool> _);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] HasBool({id}): {ex.Message}"); return false; }
        }

        private void SetBool(PropertyId<bool> id, string owner, bool targetValue, bool on, string label)
        {
            if (_db == null) return;
            try
            {
                IProperty<bool> prop = _db.GetProperty(id);
                if (prop == null) { Log.Warning($"[{CompanySupplier.ModName}] {label}: Property nicht gefunden."); return; }
                if (on) prop.AddOrSetModifier(owner, targetValue, PropertyModifiers.NO_GROUP);
                else    prop.TryRemoveModifier(owner);
                Log.Info($"[{CompanySupplier.ModName}] {label} = {on}.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] {label} umschalten: {ex.Message}"); }
        }
    }
}
