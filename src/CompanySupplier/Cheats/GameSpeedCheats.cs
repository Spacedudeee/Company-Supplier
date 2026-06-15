using System;
using Mafi;
using Mafi.Core.Input;
using Mafi.Core.Simulation;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Spielgeschwindigkeit": setzt den Simulations-Geschwindigkeits-Multiplikator ueber
    /// das normale 3x-Limit der UI hinaus (z. B. 10x/20x) und kann optional den Adaptiv-Modus auf
    /// <c>Uncapped</c> stellen (Sim laeuft so schnell wie die CPU erlaubt).
    ///
    /// 0.8.5.0 (per ApiInspector verifiziert):
    ///  - <c>GameSpeedChangeCmd(int newSpeedMultiplier)</c> nimmt einen ROHEN int -> beliebig hohe Werte
    ///    moeglich; eingeplant ueber den <see cref="IInputScheduler"/> (deterministisch/save-sicher, exakt
    ///    wie die bestehenden Flotten-Cheat-Cmds).
    ///  - <c>GameSpeedController.SetAdaptiveSimSpeedMode(SimAdaptiveSpeedMode)</c> (Unity-seitig, via DI
    ///    aufloesbar) schaltet zwischen <c>Predictive</c> (normal) und <c>Uncapped</c>.
    ///
    /// Robustheit: Manager via TryResolve, jeder Cheat in try/catch + Log.Warning.
    /// </summary>
    public sealed class GameSpeedCheats
    {
        private readonly DependencyResolver _resolver;

        private IInputScheduler _inputScheduler;

        // GameSpeedController liegt Unity-seitig; lazy aufgeloest (kann je nach Ladephase erst spaeter da sein).
        private object _speedController;
        private bool _speedControllerResolved;

        /// <summary>Zuletzt von uns gesetzter Multiplikator (fuer die UI-Spiegelung; 0 = unbekannt/Default).</summary>
        public int LastSpeedMultiplier { get; private set; }

        /// <summary>true = Adaptiv-Modus Uncapped aktiv (von uns gesetzt).</summary>
        public bool Uncapped { get; private set; }

        public GameSpeedCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IInputScheduler>(out _inputScheduler);
        }

        /// <summary>Setzt den Sim-Geschwindigkeits-Multiplikator (z. B. 1, 5, 10, 20). Werte &lt; 1 werden
        /// auf 1 angehoben.</summary>
        public void SetSpeed(int multiplier)
        {
            if (_inputScheduler == null) return;
            if (multiplier < 1) multiplier = 1;
            try
            {
                _inputScheduler.ScheduleInputCmd(new GameSpeedChangeCmd(multiplier));
                LastSpeedMultiplier = multiplier;
                Log.Info($"[{CompanySupplier.ModName}] Spielgeschwindigkeit = {multiplier}x eingeplant.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetSpeed({multiplier}): {ex.Message}");
            }
        }

        /// <summary>Schaltet den Adaptiv-Modus zwischen Uncapped (true) und Predictive (false). Best-effort:
        /// braucht den Unity-seitigen GameSpeedController, der ggf. nicht aufloesbar ist (dann No-Op).</summary>
        public void SetUncapped(bool uncapped)
        {
            try
            {
                var controller = ResolveSpeedController();
                if (controller == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] SetUncapped: GameSpeedController nicht aufgeloest — uebersprungen.");
                    return;
                }

                var mode = uncapped ? SimAdaptiveSpeedMode.Uncapped : SimAdaptiveSpeedMode.Predictive;
                // Direkter Aufruf der public Methode ueber den konkreten Typ (per Reflection, da der
                // Mafi.Unity-Typ hier nicht hart referenziert werden soll — haelt die Abhaengigkeit schlank).
                var mi = controller.GetType().GetMethod("SetAdaptiveSimSpeedMode");
                if (mi == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] SetUncapped: SetAdaptiveSimSpeedMode nicht gefunden.");
                    return;
                }
                mi.Invoke(controller, new object[] { mode });
                Uncapped = uncapped;
                Log.Info($"[{CompanySupplier.ModName}] Adaptiv-Sim-Modus = {mode}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetUncapped({uncapped}): {ex.Message}");
            }
        }

        private object ResolveSpeedController()
        {
            if (_speedControllerResolved) return _speedController;
            _speedControllerResolved = true;
            try
            {
                if (_resolver.TryResolve<Mafi.Unity.InputControl.GameSpeedController>(out var c))
                    _speedController = c;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] GameSpeedController-Aufloesung warf: {ex.Message}");
            }
            return _speedController;
        }
    }
}
