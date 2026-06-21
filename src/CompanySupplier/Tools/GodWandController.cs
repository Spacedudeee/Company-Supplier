using System;
using Mafi;
using Mafi.Core.Input;
using Mafi.Core.Buildings.Cargo;
using Mafi.Core.Buildings.Shipyard;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Vehicles;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CompanySupplier.Tools
{
    /// <summary>
    /// Universelles Welt-Klick-Werkzeug ("God-Tool"): solange aktiv, wirkt ein Linksklick je nach
    /// angeklicktem Objekt:
    ///  - Werft (<see cref="Shipyard"/>)      -> sofort volltanken (ShipyardCheatFullFuelCmd)
    ///  - Cargo-Depot (<see cref="CargoDepot"/>) -> sofort volltanken (CargoDepotCheatFullFuelCmd)
    ///  - Fahrzeug (<see cref="Vehicle"/>)     -> Tank sofort auf Maximum (FuelTank.FillToMax)
    ///
    /// Generalisierung des <see cref="StorageWandController"/>: dasselbe schlanke
    /// <see cref="IUnityInputController"/>-Muster, dasselbe Picking ueber den
    /// <see cref="CursorPickingManager"/> (generisches <c>TryPickEntity&lt;T&gt;</c>). Die beiden
    /// Volltank-Cheats laufen ueber den <see cref="IInputScheduler"/> (deterministisch/save-sicher);
    /// das Fahrzeug-Tanken nutzt die public <c>Vehicle.FuelTank</c> + <c>FuelTank.FillToMax()</c>.
    ///
    /// Registrierung per <c>[GlobalDependency(AsEverything)]</c>; Aktivierung ueber den
    /// <see cref="IUnityInputMgr"/> (getriggert vom Sandbox-Tab via CheatService).
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class GodWandController : IUnityInputController
    {
        private readonly DependencyResolver _resolver;

        // Lazy aufgeloest (kein TryResolve im Ctor -> rekursiver Resolve; vgl. StorageWandController).
        private CursorPickingManager _picker;
        private IInputScheduler _scheduler;
        private bool _resolved;

        private bool _isActive;

        public bool IsActive => _isActive;

        public GodWandController(DependencyResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>Tool-Modus: blockt Bau-/Auswahl-Eingaben des Spiels, oeffnet kein eigenes Fenster.</summary>
        public ControllerConfig Config => ControllerConfig.Tool;

        public void Activate()
        {
            _isActive = true;
            Log.Info($"[{CompanySupplier.ModName}] God-Werkzeug aktiviert (Werft/Depot/Fahrzeug anklicken zum Volltanken).");
        }

        public void Deactivate()
        {
            _isActive = false;
            Log.Info($"[{CompanySupplier.ModName}] God-Werkzeug deaktiviert.");
        }

        private void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;
            _resolver.TryResolve<CursorPickingManager>(out _picker);
            _resolver.TryResolve<IInputScheduler>(out _scheduler);
            if (_picker == null)
                Log.Warning($"[{CompanySupplier.ModName}] GodWandController: CursorPickingManager nicht aufgeloest — Werkzeug inert.");
        }

        public bool InputUpdate()
        {
            if (!_isActive) return false;
            EnsureResolved();
            if (_picker == null) return false;
            if (!Input.GetMouseButtonDown(0)) return false;

            try
            {
                // Prioritaet: Werft -> Cargo-Depot -> Fahrzeug. Erster Treffer gewinnt.
                if (_picker.TryPickEntity<Shipyard>(out var shipyard) && shipyard != null)
                {
                    _scheduler?.ScheduleInputCmd(new ShipyardCheatFullFuelCmd(shipyard.Id));
                    UI.CheatMenuStatus.Show($"Werft {shipyard.Id}: vollgetankt");
                    return true;
                }
                if (_picker.TryPickEntity<CargoDepot>(out var depot) && depot != null)
                {
                    _scheduler?.ScheduleInputCmd(new CargoDepotCheatFullFuelCmd(depot.Id));
                    UI.CheatMenuStatus.Show($"Cargo-Depot {depot.Id}: vollgetankt");
                    return true;
                }
                if (_picker.TryPickEntity<Vehicle>(out var vehicle) && vehicle != null)
                {
                    if (RefuelVehicle(vehicle))
                        UI.CheatMenuStatus.Show($"Fahrzeug {vehicle.Id}: vollgetankt");
                    else
                        UI.CheatMenuStatus.Show($"Fahrzeug {vehicle.Id}: kein Tank");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] God-Werkzeug Klick: {ex.Message}");
            }

            return false;
        }

        /// <summary>Fuellt den Tank eines Fahrzeugs auf Maximum (public FuelTank.FillToMax). Liefert true,
        /// wenn ein Tank vorhanden war.</summary>
        private static bool RefuelVehicle(Vehicle vehicle)
        {
            // Vehicle.FuelTank ist Option<IFuelTankReadonly>; das konkrete Objekt ist eine FuelTank.
            var readOnly = vehicle.FuelTank.ValueOrNull;
            if (readOnly is FuelTank tank)
            {
                tank.FillToMax();
                return true;
            }
            return false;
        }
    }
}
