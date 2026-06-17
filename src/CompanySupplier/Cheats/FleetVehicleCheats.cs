using System;
using System.Reflection;
using Mafi;
using Mafi.Collections;
using Mafi.Core;                       // IdsCore.PropertyIds, ProductQuantity (struct liegt in Mafi.Core)
using Mafi.Core.Buildings.Shipyard;
using Mafi.Core.Input;
using Mafi.Core.Products;
using Mafi.Core.PropertiesDb;
using Mafi.Core.Vehicles;
using Mafi.Core.World;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider fuer die Gruppe "Werft/Flotte + Fahrzeuge".
    ///
    /// Robustheits-Prinzip (Early-Access-API kann driften): jeder Manager wird mit TryResolve geholt
    /// und jeder Cheat in try/catch gekapselt — ein einzelner gebrochener Cheat darf weder den Mod
    /// noch das Spiel zum Absturz bringen, sondern wird nur ins Log geschrieben.
    /// </summary>
    public sealed class FleetVehicleCheats
    {
        /// <summary>Owner-Schluessel fuer unsere PropertyModifier (eindeutig, damit sie isoliert
        /// gesetzt/entfernt werden koennen, ohne fremde Modifier zu beruehren).</summary>
        private const string ModifierOwner = "CompanySupplier.FleetVehicle";

        private readonly DependencyResolver _resolver;

        // Verifiziert gegen 0.8.5.0:
        private IInputScheduler _inputScheduler;          // plant Cheat-InputCommands (Explore/Repair) ein
        private TravelingFleetManager _fleetManager;      // Zugriff auf das Welt-Schiff (BattleShip) + Dock
        private IVehiclesManager _vehiclesManager;        // Fahrzeug-Limit (public IncreaseVehicleLimit)
        private IPropertiesDb _propertiesDb;              // globale Spiel-Properties (Treibstoff/LKW-Kapazitaet)

        /// <summary>Zuletzt von uns gesetzter Treibstoff-aus-Zustand (fuer UI-Spiegelung / Zustands-Erfassung).</summary>
        public bool FuelConsumptionDisabled { get; private set; }

        public FleetVehicleCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IInputScheduler>(out _inputScheduler);
            _resolver.TryResolve<TravelingFleetManager>(out _fleetManager);
            _resolver.TryResolve<IVehiclesManager>(out _vehiclesManager);
            _resolver.TryResolve<IPropertiesDb>(out _propertiesDb);

            LogShipyardDiagnostics(); // TEMP: Werft-Member ins Log (fuer den Shipyard-Kapazitaets-Hebel)
        }

        // TEMP-Diagnose: die Werft-Klasse laesst sich NICHT per ReflectionOnly (.NET 4.8) inspizieren (DIM-Problem),
        // zur Laufzeit (Mono) aber schon. Loggt einmalig die kapazitaets-/lager-relevanten Member + Interfaces,
        // damit der Shipyard-Kapazitaets-Hebel gefunden werden kann. Wird danach wieder entfernt.
        private static void LogShipyardDiagnostics()
        {
            try
            {
                var t = typeof(Shipyard);
                Log.Info($"[{CompanySupplier.ModName}] DIAG Shipyard base={t.BaseType?.Name} ifaces=[{string.Join(", ", Array.ConvertAll(t.GetInterfaces(), i => i.Name))}]");
                const string rx = "capacit|storage|cargo|product|quant|max|limit|store|hold|buffer|stored";
                foreach (var p in t.GetProperties())
                    if (System.Text.RegularExpressions.Regex.IsMatch(p.Name, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        Log.Info($"[{CompanySupplier.ModName}] DIAG Shipyard.prop {p.PropertyType.Name} {p.Name}");
                foreach (var m in t.GetMethods())
                    if (m.DeclaringType == t && System.Text.RegularExpressions.Regex.IsMatch(m.Name, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        Log.Info($"[{CompanySupplier.ModName}] DIAG Shipyard.meth {m.ReturnType.Name} {m.Name}");
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (System.Text.RegularExpressions.Regex.IsMatch(f.Name, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        Log.Info($"[{CompanySupplier.ModName}] DIAG Shipyard.field {f.FieldType.Name} {f.Name}");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] DIAG Shipyard: {ex.Message}"); }
        }

        // ----------------------------------------------------------------------------------------
        // Werft / Flotte
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Entlaedt die gesamte Fracht des Welt-Schiffs sofort in seine zugewiesene Werft (Dock),
        /// ohne auf das Andocken/die normale Entlade-Simulation zu warten.
        /// </summary>
        /// <remarks>
        /// 0.8.5.0: <c>TravelingFleetManager.TravelingFleet</c> liefert ein <see cref="BattleShip"/>
        /// (frueher die <c>TravelingFleet</c>-Entity). Das Schiff stellt das public
        /// <c>TryUnloadCargo(Quantity, IReadOnlySet&lt;ProductProto&gt;)</c> bereit; das Dock haengt am
        /// public <c>AssignedDock</c> (Option&lt;Shipyard&gt;) und nimmt die Fracht via public
        /// <c>Shipyard.StoreProduct(ProductQuantity)</c> auf — exakt der 0.8.5.0-Nachfolger des
        /// Referenz-<c>ForceUnloadShipyardShip</c>.
        /// </remarks>
        public void ForceUnloadShip()
        {
            if (_fleetManager == null) return;
            try
            {
                if (!_fleetManager.HasFleet)
                {
                    Log.Info($"[{CompanySupplier.ModName}] ForceUnloadShip: kein Welt-Schiff vorhanden.");
                    return;
                }

                BattleShip ship = _fleetManager.TravelingFleet;
                if (ship == null) return;

                Shipyard dock = ship.AssignedDock.ValueOrNull;
                if (dock == null)
                {
                    Log.Info($"[{CompanySupplier.ModName}] ForceUnloadShip: Schiff hat keine zugewiesene Werft.");
                    return;
                }

                var skipNothing = new Set<ProductProto>();
                int loops = 0;
                while (loops++ < 1024) // harte Schleifen-Obergrenze gegen jede Haengegefahr
                {
                    ProductQuantity pq = ship.TryUnloadCargo(Quantity.MaxValue, skipNothing);
                    if (pq.IsEmpty) break;
                    dock.StoreProduct(pq);
                }
                Log.Info($"[{CompanySupplier.ModName}] ForceUnloadShip: Fracht in die Werft entladen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] ForceUnloadShip: {ex.Message}");
            }
        }

        /// <summary>Beendet die laufende Erkundung der aktuellen Welt-Karte-Position sofort.</summary>
        /// <remarks>
        /// 0.8.5.0: public parameterloser <see cref="ExploreFinishCheatCmd"/>, vom
        /// <c>TravelingFleetManager</c> als CommandProcessor verarbeitet — eingeplant ueber den
        /// <see cref="IInputScheduler"/> (deterministisch/save-sicher, statt direktem State-Poke).
        /// </remarks>
        public void FinishExploration()
        {
            if (_inputScheduler == null) return;
            try
            {
                _inputScheduler.ScheduleInputCmd(new ExploreFinishCheatCmd());
                Log.Info($"[{CompanySupplier.ModName}] FinishExploration eingeplant.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] FinishExploration: {ex.Message}");
            }
        }

        /// <summary>Repariert das Welt-Schiff (Flotte) sofort auf volle HP.</summary>
        /// <remarks>
        /// 0.8.5.0: public parameterloser <see cref="FleetRepairCheatCmd"/>, vom
        /// <c>TravelingFleetManager</c> verarbeitet — eingeplant ueber den <see cref="IInputScheduler"/>.
        /// </remarks>
        public void RepairShip()
        {
            if (_inputScheduler == null) return;
            try
            {
                _inputScheduler.ScheduleInputCmd(new FleetRepairCheatCmd());
                Log.Info($"[{CompanySupplier.ModName}] RepairShip eingeplant.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] RepairShip: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Fahrzeuge
        // ----------------------------------------------------------------------------------------

        /// <summary>Aendert das maximale Fahrzeug-Limit um <paramref name="delta"/> (positiv erhoeht,
        /// negativ verringert).</summary>
        /// <remarks>
        /// 0.8.5.0: <c>IVehiclesManager.IncreaseVehicleLimit(int diff)</c> ist public und akzeptiert
        /// auch negative Werte (Delta auf den Bonus). Direkter und robuster als ueber den
        /// <c>VehicleLimitBonus</c>-PropertyModifier.
        /// </remarks>
        public void ChangeVehicleLimit(int delta)
        {
            if (_vehiclesManager == null) return;
            try
            {
                _vehiclesManager.IncreaseVehicleLimit(delta);
                Log.Info($"[{CompanySupplier.ModName}] Fahrzeug-Limit um {delta} geaendert (uebrig: {_vehiclesManager.VehiclesLimitLeft}).");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] ChangeVehicleLimit: {ex.Message}");
            }
        }

        /// <summary>Aktuelles Gesamt-Fahrzeug-Limit (Maximum), oder -1 wenn der Manager fehlt.</summary>
        public int GetVehicleLimit() => _vehiclesManager?.MaxVehiclesLimit ?? -1;

        /// <summary>Setzt das Fahrzeug-Limit ABSOLUT auf <paramref name="newLimit"/> (>= 0). Intern ueber das
        /// Delta zur aktuellen Obergrenze, da der Manager nur <c>IncreaseVehicleLimit(diff)</c> bietet.</summary>
        public void SetVehicleLimit(int newLimit)
        {
            if (_vehiclesManager == null) return;
            if (newLimit < 0) newLimit = 0;
            try
            {
                int cur = _vehiclesManager.MaxVehiclesLimit;
                _vehiclesManager.IncreaseVehicleLimit(newLimit - cur);
                Log.Info($"[{CompanySupplier.ModName}] Fahrzeug-Limit auf {newLimit} gesetzt (war {cur}).");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetVehicleLimit({newLimit}): {ex.Message}");
            }
        }

        /// <summary>Schaltet den Treibstoff-Verbrauch von Fahrzeugen global an/aus.</summary>
        /// <remarks>
        /// 0.8.5.0: globale bool-Property <c>IdsCore.PropertyIds.FuelConsumptionDisabled</c>
        /// (registriert als <c>BooleanPropertyProto</c>). Wir setzen/loeschen einen eigenen
        /// PropertyModifier (Owner <see cref="ModifierOwner"/>) ueber das public
        /// <c>IProperty&lt;bool&gt;.AddOrSetModifier</c> / <c>TryRemoveModifier</c> — so beruehren wir
        /// keine fremden (Edict-/Forschungs-)Modifier.
        /// </remarks>
        public void SetFuelConsumptionDisabled(bool disabled)
        {
            if (_propertiesDb == null) return;
            try
            {
                IProperty<bool> prop = _propertiesDb.GetProperty(IdsCore.PropertyIds.FuelConsumptionDisabled);
                if (prop == null) return;

                if (disabled)
                    prop.AddOrSetModifier(ModifierOwner, true, PropertyModifiers.NO_GROUP);
                else
                    prop.TryRemoveModifier(ModifierOwner);

                FuelConsumptionDisabled = disabled;
                Log.Info($"[{CompanySupplier.ModName}] Treibstoff-Verbrauch deaktiviert = {disabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetFuelConsumptionDisabled: {ex.Message}");
            }
        }

        /// <summary>Setzt einen LKW-Kapazitaets-Multiplikator (in Prozent, z. B. 500 = 5x Fracht je LKW).</summary>
        /// <remarks>
        /// 0.8.5.0: globale Percent-Property <c>IdsCore.PropertyIds.TrucksCapacityMultiplier</c>
        /// (registriert als <c>PercentPropertyProto</c>). Gesetzt als eigener additiver/relativer
        /// PropertyModifier (Owner <see cref="ModifierOwner"/>); <see cref="ResetTruckCapacity"/>
        /// entfernt ihn wieder.
        /// </remarks>
        public void SetTruckCapacityMultiplier(int percent)
        {
            if (_propertiesDb == null) return;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(IdsCore.PropertyIds.TrucksCapacityMultiplier);
                if (prop == null) return;

                // WICHTIG: FromPercentVal (100 = 100%), NICHT FromInt (1 = 100%, also FromInt(100)=10000% -> x101!).
                // +100% soll die Kapazitaet verdoppeln (Modifier ist additiv auf die 100%-Basis): 180 -> 360.
                prop.AddOrSetModifier(ModifierOwner, Percent.FromPercentVal(percent), PropertyModifiers.NO_GROUP);
                Log.Info($"[{CompanySupplier.ModName}] LKW-Kapazitaets-Multiplikator = +{percent}% gesetzt.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetTruckCapacityMultiplier: {ex.Message}");
            }
        }

        /// <summary>Entfernt den von uns gesetzten LKW-Kapazitaets-Multiplikator (zurueck auf Normal).</summary>
        public void ResetTruckCapacity()
        {
            if (_propertiesDb == null) return;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(IdsCore.PropertyIds.TrucksCapacityMultiplier);
                if (prop == null) return;

                prop.TryRemoveModifier(ModifierOwner);
                Log.Info($"[{CompanySupplier.ModName}] LKW-Kapazitaets-Multiplikator zurueckgesetzt.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] ResetTruckCapacity: {ex.Message}");
            }
        }

        /// <summary>
        /// Liefert den aktuell wirksamen LKW-Kapazitaets-Multiplikator (Basis + alle Modifier, also genau
        /// der Wert, den das Spiel selbst per <c>Truck.onCapacityMultiplierChange</c> auf
        /// <c>TruckProto.CapacityBase</c> anwendet). Reset-Zustand = <see cref="Percent.Hundred"/> (=100%,
        /// keine Aenderung). Wird von der UI gebraucht, um die effektive Kapazitaet je LKW-Typ als
        /// <c>CapacityBase.ScaledBy(mult)</c> anzuzeigen — <c>_propertiesDb</c> liegt hier privat.
        /// </summary>
        /// <remarks>
        /// Robustheit: faellt bei nicht aufgeloester PropertiesDb / fehlender Property auf
        /// <see cref="Percent.Hundred"/> zurueck (UI zeigt dann unveraenderte Basis-Kapazitaet).
        /// </remarks>
        public Percent GetTruckCapacityMultiplier()
        {
            if (_propertiesDb == null) return Percent.Hundred;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(IdsCore.PropertyIds.TrucksCapacityMultiplier);
                if (prop == null) return Percent.Hundred;
                return prop.Value;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] GetTruckCapacityMultiplier: {ex.Message}");
                return Percent.Hundred;
            }
        }

        /// <summary>Hoechster je per UI setzbarer Delta-Modifier (+500%-Button ist die groesste Stufe;
        /// die Buttons setzen ABSOLUT via AddOrSetModifier, stacken also nicht). Alles oberhalb dieser
        /// Schwelle kann nur der alte Pre-Fix-Bug sein (damals Percent.FromInt -> +10000%).</summary>
        private static readonly Percent AbsurdModifierThreshold = Percent.FromPercentVal(600);

        /// <summary>
        /// Einmal-Heilung beim Fenster-Aufbau: entfernt NUR einen absurd hohen Alt-Modifier (Bug aus
        /// Pre-Fix-Sessions, damals <c>Percent.FromInt</c> -> +10000%, was die UI als "20 → 2020" zeigte).
        /// Legitime Cheat-Werte (z. B. +100/+200/+500 %) bleiben erhalten, weil sie unter der Schwelle liegen.
        /// </summary>
        /// <remarks>
        /// 0.8.5.0: <c>IProperty&lt;Percent&gt;.TryGetModifier(String owner, out PropertyModifier&lt;Percent&gt;)</c>
        /// liefert unseren Modifier mit seinem gespeicherten Delta-<c>Value</c>. Greift nur, wenn der
        /// Modifier UNS gehoert (<see cref="ModifierOwner"/>) UND sein Delta absurd gross ist — fremde
        /// (Edict-/Forschungs-)Modifier werden nie beruehrt.
        /// </remarks>
        /// <returns><c>true</c>, wenn ein Alt-Modifier entfernt wurde (UI sollte dann refreshen).</returns>
        public bool SanitizeTruckCapacityIfAbsurd()
        {
            if (_propertiesDb == null) return false;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(IdsCore.PropertyIds.TrucksCapacityMultiplier);
                if (prop == null) return false;

                if (prop.TryGetModifier(ModifierOwner, out PropertyModifier<Percent> mine)
                    && mine.Value > AbsurdModifierThreshold)
                {
                    prop.TryRemoveModifier(ModifierOwner);
                    Log.Info($"[{CompanySupplier.ModName}] Alt-LKW-Kapazitaets-Modifier ({mine.Value}) bereinigt (> {AbsurdModifierThreshold}).");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SanitizeTruckCapacityIfAbsurd: {ex.Message}");
            }
            return false;
        }
    }
}
