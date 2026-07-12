using System;
using System.Collections;
using System.Reflection;
using Mafi;
using Mafi.Core.Input;                    // IInputScheduler, InputCommand
using Mafi.Core.SpaceProgram;             // *Cmd, RocketLaunchManager
using Mafi.Core.Buildings.SpaceProgram;   // RocketLaunchPad

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Weltraum": One-Shot-Aktionen fuer das Raumfahrtprogramm. Alles ueber schedulbare
    /// <see cref="InputCommand"/>s (deterministisch/save-sicher, wie <c>FleetVehicleCheats</c> Explore/Repair):
    ///  - Raumstation bauen/ausbauen : <see cref="BuildOrUpgradeSpaceStationCmd"/> (parameterlos).
    ///  - Neuen Asteroiden entdecken : <see cref="ScanForNewAsteroidCmd"/> (parameterlos; ueberspringt die Scan-Zeit).
    ///  - Alle Raketen starten        : je Startrampe ein <see cref="LaunchRocketCmd"/> (das Spiel verwirft
    ///    Rampen ohne startbereite Rakete selbst — daher unkritisch alle durchschicken).
    ///
    /// Die Startrampen sind nicht public enumerierbar (<c>IRocketLaunchManager</c> bietet keine Liste), daher
    /// wird das private <c>RocketLaunchManager.m_launchPads</c> (Set) per Reflection gelesen.
    ///
    /// Robustheit: Manager via TryResolve; jede Aktion null-guarded + try/catch + Log.Warning.
    /// </summary>
    public sealed class SpaceCheats
    {
        private const BindingFlags InstAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly DependencyResolver _resolver;
        private IInputScheduler _scheduler;       // plant die InputCommands ein
        private RocketLaunchManager _rocketMgr;   // Zugriff auf die Startrampen (m_launchPads, privat)

        public SpaceCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IInputScheduler>(out _scheduler);
            _resolver.TryResolve<RocketLaunchManager>(out _rocketMgr);
        }

        /// <summary>Baut die Raumstation bzw. hebt sie auf die naechste Stufe (eine Stufe pro Klick).</summary>
        public void BuildOrUpgradeStation()
        {
            if (_scheduler == null) return;
            try
            {
                _scheduler.ScheduleInputCmd(new BuildOrUpgradeSpaceStationCmd());
                Log.Info($"[{CompanySupplier.ModName}] Raumstation bauen/ausbauen eingeplant.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] BuildOrUpgradeStation: {ex.Message}"); }
        }

        /// <summary>Entdeckt sofort einen neuen Asteroiden (ueberspringt die Scan-Wartezeit).</summary>
        public void ScanForAsteroid()
        {
            if (_scheduler == null) return;
            try
            {
                _scheduler.ScheduleInputCmd(new ScanForNewAsteroidCmd());
                Log.Info($"[{CompanySupplier.ModName}] Asteroiden-Scan eingeplant.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ScanForAsteroid: {ex.Message}"); }
        }

        /// <summary>Startet auf JEDER Startrampe eine Rakete (Rampen ohne startbereite Rakete verwirft das Spiel selbst).</summary>
        public void LaunchAllRockets()
        {
            if (_scheduler == null || _rocketMgr == null) return;
            try
            {
                FieldInfo fi = typeof(RocketLaunchManager).GetField("m_launchPads", InstAll);
                if (!(fi?.GetValue(_rocketMgr) is IEnumerable pads))
                {
                    Log.Warning($"[{CompanySupplier.ModName}] LaunchAllRockets: Startrampen nicht lesbar (API-Drift?).");
                    return;
                }
                int count = 0;
                foreach (var pad in pads)
                {
                    if (!(pad is RocketLaunchPad p)) continue;
                    try { _scheduler.ScheduleInputCmd(new LaunchRocketCmd(p.Id)); count++; }
                    catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] LaunchRocket({p?.Id}): {ex.Message}"); }
                }
                Log.Info($"[{CompanySupplier.ModName}] Raketen-Start auf {count} Rampe(n) eingeplant.");
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] LaunchAllRockets: {ex.Message}"); }
        }
    }
}
