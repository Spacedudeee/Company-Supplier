using System;
using System.Reflection;
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

        // "Uncapped" = angehobener Speed-Cap; in der Praxis limitiert die CPU vorher (echtes "so schnell wie moeglich").
        // Das Spiel-Hardcap ist 12x (SUPER_FAST_FORWARD_MULT) — wir heben den hoechsten Speeds-Eintrag hierauf an.
        private const int UncappedMultiplier = 100;
        private const BindingFlags InstAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Original-Wert des hoechsten Speeds-Eintrags (Spiel-Super-Speed, normal 12x) — fuer Reset.
        private int? _origSuperSpeed;

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
                RaiseSpeedCapTo(multiplier); // Hardcap (12x) anheben -> echte 20x statt geklemmter 12x
                _inputScheduler.ScheduleInputCmd(new GameSpeedChangeCmd(multiplier));
                LastSpeedMultiplier = multiplier;
                Log.Info($"[{CompanySupplier.ModName}] Spielgeschwindigkeit = {multiplier}x eingeplant.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetSpeed({multiplier}): {ex.Message}");
            }
        }

        /// <summary>Schaltet "so schnell wie die CPU kann" an/aus.
        /// WICHTIG: Das Spiel deckelt die Sim-Geschwindigkeit HART auf den hoechsten Eintrag im
        /// <c>Speeds</c>-Array (Super-Speed = 12x, <c>SUPER_FAST_FORWARD_MULT</c>) — ein hoeheres Sim-Ziel allein
        /// wird darauf geklemmt (deshalb lief der "20x"-Button real nur 12x). Daher heben wir diesen Eintrag per
        /// Reflection auf <see cref="UncappedMultiplier"/> an, setzen den Adaptiv-Modus auf Uncapped (keine
        /// FPS-Drossel) und das Sim-Ziel auf denselben Wert. Aus -> Original wiederherstellen, Predictive, 1x.
        /// Best effort: braucht den Unity-GameSpeedController.</summary>
        public void SetUncapped(bool uncapped)
        {
            try
            {
                if (uncapped) RaiseSpeedCapTo(UncappedMultiplier);

                // Adaptiv-Modus: Uncapped = Sim nicht fuer FPS drosseln, flat out laufen.
                var controller = ResolveSpeedController();
                if (controller != null)
                {
                    var mode = uncapped ? SimAdaptiveSpeedMode.Uncapped : SimAdaptiveSpeedMode.Predictive;
                    var mi = controller.GetType().GetMethod("SetAdaptiveSimSpeedMode");
                    if (mi != null) mi.Invoke(controller, new object[] { mode });
                }
                else
                {
                    Log.Warning($"[{CompanySupplier.ModName}] SetUncapped: GameSpeedController nicht aufgeloest (nur der Speed-Teil greift).");
                }

                // Sim-Ziel auf den (angehobenen) Hoechstwert bzw. zurueck auf 1x; beim Ausschalten Cap zuruecksetzen.
                SetSpeed(uncapped ? UncappedMultiplier : 1);
                if (!uncapped) RestoreSpeedCap();

                Uncapped = uncapped;
                Log.Info($"[{CompanySupplier.ModName}] Uncapped = {uncapped} (Cap/Ziel {(uncapped ? UncappedMultiplier : 1)}x).");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetUncapped({uncapped}): {ex.Message}");
            }
        }

        // Hebt den hoechsten Speeds-Eintrag (Spiel-Hardcap, normal 12x = SUPER_FAST_FORWARD_MULT) auf mindestens
        // <paramref name="target"/> an, damit Multiplikatoren > 12 (echte 20x, Uncapped) nicht auf 12 geklemmt
        // werden. Snapshottet den Originalwert EINMALIG fuer <see cref="RestoreSpeedCap"/>.
        private void RaiseSpeedCapTo(int target)
        {
            try
            {
                var controller = ResolveSpeedController();
                var speeds = controller?.GetType().GetField("Speeds", InstAll)?.GetValue(controller) as int[];
                if (speeds == null || speeds.Length == 0) return;
                int maxIdx = 0;
                for (int i = 1; i < speeds.Length; i++) if (speeds[i] > speeds[maxIdx]) maxIdx = i;
                if (_origSuperSpeed == null) _origSuperSpeed = speeds[maxIdx];
                if (speeds[maxIdx] < target) speeds[maxIdx] = target;
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] RaiseSpeedCapTo({target}): {ex.Message}"); }
        }

        // Stellt den originalen Spiel-Hardcap (12x) wieder her (z. B. nach Uncapped-Aus).
        private void RestoreSpeedCap()
        {
            if (_origSuperSpeed == null) return;
            try
            {
                var controller = ResolveSpeedController();
                var speeds = controller?.GetType().GetField("Speeds", InstAll)?.GetValue(controller) as int[];
                if (speeds != null && speeds.Length > 0)
                {
                    int maxIdx = 0;
                    for (int i = 1; i < speeds.Length; i++) if (speeds[i] > speeds[maxIdx]) maxIdx = i;
                    speeds[maxIdx] = _origSuperSpeed.Value;
                }
                _origSuperSpeed = null;
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] RestoreSpeedCap: {ex.Message}"); }
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
