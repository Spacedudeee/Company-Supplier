using System;
using Mafi;
using Mafi.Core;                       // IdsCore.PropertyIds, PropertyId<T>
using Mafi.Core.PropertiesDb;          // IPropertiesDb, IProperty<T>, PropertyModifiers, PropertyModifier<T>

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
    /// </summary>
    public sealed class GameplayCheats
    {
        private const string OwnerTrainsFuel  = "CompanySupplier.Gameplay.TrainsFuel";
        private const string OwnerFreeBuild   = "CompanySupplier.Gameplay.FreeBuild";
        private const string OwnerMachinePow  = "CompanySupplier.Gameplay.MachinePower";
        private const string OwnerMachineComp = "CompanySupplier.Gameplay.MachineComputing";
        private const string OwnerNoConsume   = "CompanySupplier.Gameplay.NoConsumption";
        private const string OwnerHousing     = "CompanySupplier.Gameplay.Housing";

        private readonly DependencyResolver _resolver;
        private IPropertiesDb _db;

        public GameplayCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _db);
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

        // ---- generische Helfer (ein IProperty<Percent>-Cache pro Aufruf; robust) ------------------

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
    }
}
