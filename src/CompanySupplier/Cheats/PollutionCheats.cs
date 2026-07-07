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

        // Fallback-Werte, falls die PropertiesDb nicht lesbar ist (zuletzt von uns gesetzter Zustand).
        private bool _airCached, _waterCached, _landfillCached, _vehiclesCached, _shipsCached, _trainsCached;

        // Status live aus der Praesenz UNSERES Modifiers gelesen: PropertyModifier ueberleben im
        // Spielstand (vgl. FleetVehicleCheats.SanitizeTruckCapacityIfAbsurd) — reine Session-Mirror
        // wuerden nach einem Save-Load luegen (Cheat aktiv, UI/Zustands-Erfassung sagt aus).
        public bool AirDisabled      => HasOurModifier(IdsCore.PropertyIds.AirPollutionMultiplier, _airCached);
        public bool WaterDisabled    => HasOurModifier(IdsCore.PropertyIds.WaterPollutionMultiplier, _waterCached);
        public bool LandfillDisabled => HasOurModifier(IdsCore.PropertyIds.LandfillPollutionMultiplier, _landfillCached);
        public bool VehiclesDisabled => HasOurModifier(IdsCore.PropertyIds.VehiclesPollutionMultiplier, _vehiclesCached);
        public bool ShipsDisabled    => HasOurModifier(IdsCore.PropertyIds.ShipsPollutionMultiplier, _shipsCached);
        public bool TrainsDisabled   => HasOurModifier(IdsCore.PropertyIds.TrainsPollutionMultiplier, _trainsCached);

        /// <summary>True, wenn die Property aktuell UNSEREN Modifier traegt (Fallback bei Lesefehler).</summary>
        private bool HasOurModifier(PropertyId<Percent> id, bool fallback)
        {
            try
            {
                IProperty<Percent> prop = _propertiesDb?.GetProperty(id);
                if (prop != null) return prop.TryGetModifier(ModifierOwner, out PropertyModifier<Percent> _);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Verschmutzungs-Status lesen: {ex.Message}");
            }
            return fallback;
        }

        public PollutionCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IPropertiesDb>(out _propertiesDb);
        }

        // -- Einzelschalter je Verschmutzungsquelle -----------------------------------------------

        public void SetAirDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.AirPollutionMultiplier, disabled, "Luft")) _airCached = disabled;
        }

        public void SetWaterDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.WaterPollutionMultiplier, disabled, "Wasser")) _waterCached = disabled;
        }

        public void SetLandfillDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.LandfillPollutionMultiplier, disabled, "Deponie")) _landfillCached = disabled;
        }

        public void SetVehiclesDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.VehiclesPollutionMultiplier, disabled, "Fahrzeuge")) _vehiclesCached = disabled;
        }

        public void SetShipsDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.ShipsPollutionMultiplier, disabled, "Schiffe")) _shipsCached = disabled;
        }

        public void SetTrainsDisabled(bool disabled)
        {
            if (Apply(IdsCore.PropertyIds.TrainsPollutionMultiplier, disabled, "Züge")) _trainsCached = disabled;
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

                // Annahme: die Basis dieser Multiplikatoren ist 100 % und sonst greifen keine weiteren
                // NEGATIVEN Modifier — dann bringt -100 % den Effektivwert exakt auf 0 %. Sollten
                // Edicts/Forschung negativ eingreifen, koennte der Wert unter 0 % fallen (das Spiel
                // klemmt Prozente selbst; hier nur dokumentiert, nicht kompensierbar per Delta).
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
