using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Core;                       // IdsCore.PropertyIds, PropertyId<T>
using Mafi.Core.PropertiesDb;          // IPropertiesDb, IProperty<T>, PropertyModifiers, PropertyModifier<T>

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
    ///
    /// Robustheit: PropertiesDb via TryResolve; jeder Zugriff null-guarded + try/catch + Log.Warning.
    /// </summary>
    public sealed class BoostCheats
    {
        // Distinkter Owner je Hebel -> Reset kollidiert nie mit fremden Modifiern oder anderen CS-Domains.
        private const string OwnerMining   = "CompanySupplier.Boost.Mining";
        private const string OwnerFarm     = "CompanySupplier.Boost.Farm";
        private const string OwnerSolar    = "CompanySupplier.Boost.Solar";
        private const string OwnerForceRun = "CompanySupplier.Boost.ForceRun";
        private const string OwnerWater    = "CompanySupplier.Boost.Water";

        private readonly DependencyResolver _resolver;
        private IPropertiesDb _db;

        public BoostCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _db);
        }

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
