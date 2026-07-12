using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core;                       // IdsCore.PropertyIds, PropertyId<T>
using Mafi.Core.PropertiesDb;          // IPropertiesDb, IProperty<T>, PropertyModifiers, PropertyModifier<T>
using Mafi.Core.Prototypes;            // ProtosDb (Sonderfall Zug-Leistung: direkter Proto-Edit)
using Mafi.Core.Trains;                // LocomotiveProto (EnginePowerKw)

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Gameplay-Hebel": diverse Dauer-Toggles ueber globale <c>IdsCore.PropertyIds</c>-Modifier
    /// (dasselbe Muster wie <see cref="BoostCheats"/> — additiver Modifier je eigenem Owner, Reset entfernt nur
    /// unseren Beitrag, Status live aus der Modifier-Praesenz).
    ///
    /// Hebel (alle gegen 0.8.5.0 verifiziert):
    ///  - Zuege: kein Treibstoff       : TrainsFuelConsumptionMultiplier (-100%) — schliesst die Luecke zu LKW/Schiff.
    ///  - Gratis-Bau (0 Materialkosten): ConstructionCostsMultiplier      (-100%) — ergaenzt Instant-Build.
    ///  - Volle Leistung bei Strommangel   : MachineSpeedOnLowPower     (+100% -> deckelt auf volle Leistung).
    ///  - Volle Leistung bei Computing-Mangel: MachineSpeedOnLowComputing(+100%).
    ///  - Siedlungen brauchen keine Gueter : SettlementConsumptionMultiplier (-100% -> 0% Verbrauch).
    ///  - Wohnkapazitaet x10              : HousingCapacityMultiplier   (+900%).
    ///
    /// Robustheit: PropertiesDb via TryResolve; jeder Zugriff null-guarded + try/catch + Log.Warning.
    ///
    /// Ausnahme "Zug-Leistung x10": der PropertyId <c>TrainPowerMultiplier</c> ist in 0.8.5.0 zwar in
    /// <c>IdsCore.PropertyIds</c> deklariert, aber NICHT in der Laufzeit-PropertiesDb registriert
    /// (<c>GetProperty</c> wirft "Key not found") -> stattdessen direkter Proto-Edit auf
    /// <c>LocomotiveProto.EnginePowerKw</c> (ProtosDb + Reflection + Snapshot/Reset), Muster wie
    /// <see cref="TerrainCheats.SetUnlimitedFertility"/>.
    /// </summary>
    public sealed class GameplayCheats
    {
        private const string OwnerTrainsFuel  = "CompanySupplier.Gameplay.TrainsFuel";
        private const string OwnerFreeBuild   = "CompanySupplier.Gameplay.FreeBuild";
        private const string OwnerMachinePow  = "CompanySupplier.Gameplay.MachinePower";
        private const string OwnerMachineComp = "CompanySupplier.Gameplay.MachineComputing";
        private const string OwnerNoConsume   = "CompanySupplier.Gameplay.NoConsumption";
        private const string OwnerHousing     = "CompanySupplier.Gameplay.Housing";
        private const string OwnerMaintCons   = "CompanySupplier.Gameplay.MaintConsume";
        private const string OwnerFarmWater   = "CompanySupplier.Gameplay.FarmWater";
        private const string OwnerUnityProd   = "CompanySupplier.Gameplay.UnityProd";
        private const string OwnerTrainSlope  = "CompanySupplier.Gameplay.TrainSlope";
        private const string OwnerLogiPower   = "CompanySupplier.Gameplay.LogisticsPower";
        private const string OwnerRecycling   = "CompanySupplier.Gameplay.Recycling";
        private const string OwnerTreeGrowth  = "CompanySupplier.Gameplay.TreeGrowth";
        private const string OwnerRocketCap   = "CompanySupplier.Gameplay.RocketCap";
        private const string OwnerRainYield   = "CompanySupplier.Gameplay.RainYield";
        private const string OwnerBaseHealth  = "CompanySupplier.Gameplay.BaseHealth";
        private const string OwnerDeconRefund = "CompanySupplier.Gameplay.DeconRefund";
        private const string OwnerResearchSpd = "CompanySupplier.Gameplay.ResearchSpeed";
        private const string OwnerUnityCap    = "CompanySupplier.Gameplay.UnityCapacity";
        private const string OwnerNoBreakSlow = "CompanySupplier.Gameplay.NoBreakSlow";

        private readonly DependencyResolver _resolver;
        private IPropertiesDb _db;
        private ProtosDb _protos;

        // Original-Motorleistung je LocomotiveProto-Id (gesnapshottet VOR dem Zug-Leistung-Override) -> Reset.
        private readonly Dictionary<string, MechPower> _origLocoPower = new Dictionary<string, MechPower>();

        public GameplayCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _db);
            _resolver.TryResolve<ProtosDb>(out _protos);
        }

        // ---- oeffentliche Hebel -------------------------------------------------------------------

        public bool TrainsNoFuel             => HasPercent(IdsCore.PropertyIds.TrainsFuelConsumptionMultiplier, OwnerTrainsFuel);
        public void SetTrainsNoFuel(bool v)    => SetPercent(IdsCore.PropertyIds.TrainsFuelConsumptionMultiplier, OwnerTrainsFuel, -100, v, "Zuege: kein Treibstoff");

        public bool FreeBuild                => HasPercent(IdsCore.PropertyIds.ConstructionCostsMultiplier, OwnerFreeBuild);
        public void SetFreeBuild(bool v)       => SetPercent(IdsCore.PropertyIds.ConstructionCostsMultiplier, OwnerFreeBuild, -100, v, "Gratis-Bau (0 Materialkosten)");

        public bool MachineFullOnLowPower    => HasPercent(IdsCore.PropertyIds.MachineSpeedOnLowPower, OwnerMachinePow);
        public void SetMachineFullOnLowPower(bool v) => SetPercent(IdsCore.PropertyIds.MachineSpeedOnLowPower, OwnerMachinePow, 100, v, "Volle Leistung bei Strommangel");

        public bool MachineFullOnLowComputing => HasPercent(IdsCore.PropertyIds.MachineSpeedOnLowComputing, OwnerMachineComp);
        public void SetMachineFullOnLowComputing(bool v) => SetPercent(IdsCore.PropertyIds.MachineSpeedOnLowComputing, OwnerMachineComp, 100, v, "Volle Leistung bei Computing-Mangel");

        public bool NoSettlementConsumption  => HasPercent(IdsCore.PropertyIds.SettlementConsumptionMultiplier, OwnerNoConsume);
        public void SetNoSettlementConsumption(bool v) => SetPercent(IdsCore.PropertyIds.SettlementConsumptionMultiplier, OwnerNoConsume, -100, v, "Siedlungen brauchen keine Gueter");

        public bool HousingCapacityBoost     => HasPercent(IdsCore.PropertyIds.HousingCapacityMultiplier, OwnerHousing);
        public void SetHousingCapacityBoost(bool v) => SetPercent(IdsCore.PropertyIds.HousingCapacityMultiplier, OwnerHousing, 900, v, "Wohnkapazitaet x10");

        // ---- Welle 2 ------------------------------------------------------------------------------

        public bool NoMaintenanceConsumption => HasPercent(IdsCore.PropertyIds.MaintenanceConsumptionMultiplier, OwnerMaintCons);
        public void SetNoMaintenanceConsumption(bool v) => SetPercent(IdsCore.PropertyIds.MaintenanceConsumptionMultiplier, OwnerMaintCons, -100, v, "Kein Wartungsverbrauch");

        public bool NoFarmWater              => HasPercent(IdsCore.PropertyIds.FarmWaterConsumptionMultiplier, OwnerFarmWater);
        public void SetNoFarmWater(bool v)     => SetPercent(IdsCore.PropertyIds.FarmWaterConsumptionMultiplier, OwnerFarmWater, -100, v, "Farmen ohne Wasser");

        public bool UnityProductionBoost     => HasPercent(IdsCore.PropertyIds.UnityProductionMultiplier, OwnerUnityProd);
        public void SetUnityProductionBoost(bool v) => SetPercent(IdsCore.PropertyIds.UnityProductionMultiplier, OwnerUnityProd, 900, v, "Unity-Produktion x10");

        // Zug-Leistung x10: Sonderfall (dead PropertyId in 0.8.5.0) -> direkter Proto-Edit, s. Klassen-Doku.
        public bool TrainPowerBoost          => _origLocoPower.Count > 0;
        public void SetTrainPowerBoost(bool v) => SetLocomotivePowerBoost(v);

        public bool TrainsIgnoreSlopes       => HasPercent(IdsCore.PropertyIds.TrainSlopeDifficultyMultiplier, OwnerTrainSlope);
        public void SetTrainsIgnoreSlopes(bool v) => SetPercent(IdsCore.PropertyIds.TrainSlopeDifficultyMultiplier, OwnerTrainSlope, -100, v, "Zuege: Steigungen ignorieren");

        public bool LogisticsIgnorePower     => HasBool(IdsCore.PropertyIds.LogisticsIgnorePower, OwnerLogiPower);
        public void SetLogisticsIgnorePower(bool v) => SetBool(IdsCore.PropertyIds.LogisticsIgnorePower, OwnerLogiPower, true, v, "Logistik ignoriert Strom");

        public bool RecyclingFull            => HasPercent(IdsCore.PropertyIds.RecyclingRatioDiff, OwnerRecycling);
        public void SetRecyclingFull(bool v)   => SetPercent(IdsCore.PropertyIds.RecyclingRatioDiff, OwnerRecycling, 100, v, "Recycling voll");

        public bool TreeGrowthBoost          => HasPercent(IdsCore.PropertyIds.TreesGrowthSpeed, OwnerTreeGrowth);
        public void SetTreeGrowthBoost(bool v) => SetPercent(IdsCore.PropertyIds.TreesGrowthSpeed, OwnerTreeGrowth, 900, v, "Baum-Wachstum x10");

        public bool RocketCapacityBoost      => HasPercent(IdsCore.PropertyIds.RocketsCapacityMultiplier, OwnerRocketCap);
        public void SetRocketCapacityBoost(bool v) => SetPercent(IdsCore.PropertyIds.RocketsCapacityMultiplier, OwnerRocketCap, 900, v, "Raketen-Kapazitaet x10");

        public bool RainYieldBoost           => HasPercent(IdsCore.PropertyIds.RainYieldMultiplier, OwnerRainYield);
        public void SetRainYieldBoost(bool v)  => SetPercent(IdsCore.PropertyIds.RainYieldMultiplier, OwnerRainYield, 900, v, "Regen-Ertrag x10");

        // ---- Abschluss-Welle ----------------------------------------------------------------------

        public bool BaseHealthBoost          => HasPercent(IdsCore.PropertyIds.BaseHealthMultiplier, OwnerBaseHealth);
        public void SetBaseHealthBoost(bool v) => SetPercent(IdsCore.PropertyIds.BaseHealthMultiplier, OwnerBaseHealth, 100, v, "Basis-Gesundheit x2");

        public bool DeconstructionRefundBoost => HasPercent(IdsCore.PropertyIds.DeconstructionRefundMultiplier, OwnerDeconRefund);
        public void SetDeconstructionRefundBoost(bool v) => SetPercent(IdsCore.PropertyIds.DeconstructionRefundMultiplier, OwnerDeconRefund, 400, v, "Abriss-Rueckerstattung x5");

        public bool ResearchSpeedBoost       => HasPercent(IdsCore.PropertyIds.ResearchStepsMultiplier, OwnerResearchSpd);
        public void SetResearchSpeedBoost(bool v) => SetPercent(IdsCore.PropertyIds.ResearchStepsMultiplier, OwnerResearchSpd, 900, v, "Forschungs-Tempo x10");

        public bool UnityCapacityBoost       => HasPercent(IdsCore.PropertyIds.UnityCapacityMultiplier, OwnerUnityCap);
        public void SetUnityCapacityBoost(bool v) => SetPercent(IdsCore.PropertyIds.UnityCapacityMultiplier, OwnerUnityCap, 900, v, "Unity-Kapazitaet x10");

        // Maschinen verlieren bei Defekt kein Tempo: das bool-Property SlowDownIfBroken auf false zwingen.
        public bool NoSlowdownWhenBroken     => HasBool(IdsCore.PropertyIds.SlowDownIfBroken, OwnerNoBreakSlow);
        public void SetNoSlowdownWhenBroken(bool v) => SetBool(IdsCore.PropertyIds.SlowDownIfBroken, OwnerNoBreakSlow, false, v, "Kein Tempoverlust bei Defekt");

        // ---- Sonderfall Zug-Leistung (Proto-Edit statt PropertyId, s. Klassen-Doku) ---------------

        /// <summary>Zug-Leistung x10: skaliert je <see cref="LocomotiveProto"/> das (readonly) Feld
        /// <c>EnginePowerKw</c> per Reflection auf das Zehnfache -> hoehere Zugkraft (fliesst ueber
        /// <c>ComputeTractiveEffort</c> ein). Snapshot beim ersten Edit -> Reset stellt das Original wieder her.
        /// Protos werden je Sitzung neu gebaut (nicht im Save), daher In-Memory-Snapshot.</summary>
        private void SetLocomotivePowerBoost(bool on)
        {
            if (_protos == null) return;
            try
            {
                foreach (var loco in _protos.Filter<LocomotiveProto>(_ => true))
                {
                    FieldInfo fi = loco.GetType().GetField("EnginePowerKw",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi == null) continue;
                    string id = loco.Id.ToString();
                    if (on)
                    {
                        if (!_origLocoPower.ContainsKey(id)) _origLocoPower[id] = (MechPower)fi.GetValue(loco);
                        fi.SetValue(loco, _origLocoPower[id] * 10);   // x10 Motorleistung
                    }
                    else if (_origLocoPower.TryGetValue(id, out MechPower orig))
                    {
                        fi.SetValue(loco, orig);
                    }
                }
                if (!on) _origLocoPower.Clear();
                Log.Info($"[{CompanySupplier.ModName}] Zug-Leistung x10 = {on}.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] SetLocomotivePowerBoost: {ex.Message}"); }
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
