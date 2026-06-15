using System;
using Mafi;
using Mafi.Core;                       // IdsCore.PropertyIds, PropertyId<T>
using Mafi.Core.PropertiesDb;          // IPropertiesDb, IProperty<T>, PropertyModifiers

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Umwelt / Verschmutzung": deaktiviert die Verschmutzungs-Ausstoesse, indem die
    /// globalen Verschmutzungs-Multiplikatoren auf 0 % gesetzt werden. Deckt alle sechs Quellen ab:
    /// Luft, Wasser, Deponie sowie Fahrzeuge, Schiffe und Zuege.
    ///
    /// 0.8.5.0 (per ApiInspector verifiziert): die Multiplikatoren sind globale
    /// <c>PropertyId&lt;Percent&gt;</c> in <see cref="IdsCore.PropertyIds"/>. Modifier sind rein additive
    /// Deltas (<c>PropertyModifiers.Delta</c>), und <see cref="Percent"/> erlaubt negative Werte — daher
    /// bringt ein Delta von <c>-100 %</c> den Basiswert (100 %) auf 0 % (= kein Ausstoss). Exakt das
    /// erprobte Muster aus <see cref="FleetVehicleCheats.SetTruckCapacityMultiplier"/>, nur mit negativem
    /// Vorzeichen. Reset entfernt unseren Modifier wieder (fremde Edict-/Forschungs-Modifier bleiben unberuehrt).
    ///
    /// Robustheit: Manager via TryResolve, jeder Cheat in try/catch + Log.Warning.
    /// </summary>
    public sealed class PollutionCheats
    {
        /// <summary>Owner-Schluessel fuer unsere PropertyModifier (isoliert von fremden Modifiern).</summary>
        private const string ModifierOwner = "CompanySupplier.Pollution";

        private readonly DependencyResolver _resolver;
        private IPropertiesDb _propertiesDb;

        public bool AirDisabled { get; private set; }
        public bool WaterDisabled { get; private set; }
        public bool LandfillDisabled { get; private set; }
        public bool VehiclesDisabled { get; private set; }
        public bool ShipsDisabled { get; private set; }
        public bool TrainsDisabled { get; private set; }

        public PollutionCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _propertiesDb);
        }

        // -- Einzelschalter je Verschmutzungsquelle -----------------------------------------------

        public void SetAirDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.AirPollutionMultiplier, disabled, "Luft")) AirDisabled = disabled;
        }

        public void SetWaterDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.WaterPollutionMultiplier, disabled, "Wasser")) WaterDisabled = disabled;
        }

        public void SetLandfillDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.LandfillPollutionMultiplier, disabled, "Deponie")) LandfillDisabled = disabled;
        }

        public void SetVehiclesDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.VehiclesPollutionMultiplier, disabled, "Fahrzeuge")) VehiclesDisabled = disabled;
        }

        public void SetShipsDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.ShipsPollutionMultiplier, disabled, "Schiffe")) ShipsDisabled = disabled;
        }

        public void SetTrainsDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.TrainsPollutionMultiplier, disabled, "Züge")) TrainsDisabled = disabled;
        }

        /// <summary>Schaltet alle sechs Verschmutzungsquellen auf einmal.</summary>
        public void SetAllDisabled(bool disabled)
        {
            SetAirDisabled(disabled);
            SetWaterDisabled(disabled);
            SetLandfillDisabled(disabled);
            SetVehiclesDisabled(disabled);
            SetShipsDisabled(disabled);
            SetTrainsDisabled(disabled);
            Log.Info($"[{CompanySupplier.ModName}] Verschmutzung gesamt deaktiviert = {disabled}.");
        }

        // -- Kern ---------------------------------------------------------------------------------

        /// <summary>Setzt/entfernt fuer die gegebene Multiplikator-Property unseren -100%-Modifier.
        /// Liefert true bei Erfolg (fuer das Status-Mirroring der UI).</summary>
        private bool Apply(PropertyId<Percent> id, bool disabled, string label)
        {
            if (_propertiesDb == null) return false;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(id);
                if (prop == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] Verschmutzung {label}: Property nicht gefunden (API-Drift?).");
                    return false;
                }

                if (disabled)
                    prop.AddOrSetModifier(ModifierOwner, Percent.FromPercentVal(-100), PropertyModifiers.NO_GROUP);
                else
                    prop.TryRemoveModifier(ModifierOwner);

                Log.Info($"[{CompanySupplier.ModName}] Verschmutzung {label} deaktiviert = {disabled}.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Verschmutzung {label} umschalten: {ex.Message}");
                return false;
            }
        }
    }
}
