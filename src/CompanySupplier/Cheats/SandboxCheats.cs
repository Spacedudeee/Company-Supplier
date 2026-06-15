using System;
using System.Reflection;
using Mafi;
using Mafi.Core.Buildings.Settlements;
using Mafi.Core.Factory.ComputingPower;
using Mafi.Core.Factory.ElectricPower;
using Mafi.Core.Population;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider fuer die Gruppe "Sandbox / Kreativmodus": setzt die "Ignore-Missing"-Flags des
    /// Spiels, damit Maschinen/Gebaeude laufen, obwohl ihnen Strom, Arbeiter, Computing, Unity oder
    /// Lebensmittel fehlen. Zusammen mit Sofortbau + Treibstoff-aus (in anderen Providern) ergibt der
    /// Master-Schalter einen echten Kreativ-Modus.
    ///
    /// 0.8.5.0-Hinweise (per ApiInspector/Mono.Cecil gegen die Game-DLLs verifiziert): jeder Manager
    /// traegt eine [internal] <c>Cheat_IgnoreMissingX(bool)</c>-Methode (NICHT aus dem Mod-Assembly
    /// aufrufbar) -> Aufruf per Reflection. Bewusst NICHT ueber den <c>SandboxManager</c>, weil dessen
    /// Verfuegbarkeit in einem normal gestarteten Save (nicht-Sandbox) ungeklaert ist; die Einzel-Manager
    /// sind dagegen immer Teil des DI-Containers.
    ///
    /// Robustheit (Early-Access-API kann driften): jeder Manager via TryResolve, jeder Cheat in
    /// try/catch + Log.Warning; fehlt ein Manager, wird der jeweilige Cheat still zum No-Op.
    /// </summary>
    public sealed class SandboxCheats
    {
        private readonly DependencyResolver _resolver;

        // Verifiziert gegen 0.8.5.0:
        private ElectricityManager _electricity;   // Cheat_IgnoreMissingPower(bool)
        private WorkersManager _workers;           // Cheat_IgnoreMissingWorkers(bool)
        private ComputingManager _computing;       // Cheat_IgnoreMissingComputing(bool)
        private UpointsManager _upoints;           // Cheat_IgnoreMissingUnity(bool)
        private SettlementsManager _settlements;   // Cheat_IgnoreMissingFood(bool)

        /// <summary>true = Strommangel wird ignoriert (Maschinen laufen ohne Strom weiter).</summary>
        public bool NoPowerNeeded { get; private set; }
        /// <summary>true = fehlende Arbeiter werden ignoriert.</summary>
        public bool NoWorkersNeeded { get; private set; }
        /// <summary>true = fehlendes Computing wird ignoriert.</summary>
        public bool NoComputingNeeded { get; private set; }
        /// <summary>true = fehlende Unity wird ignoriert.</summary>
        public bool NoUnityNeeded { get; private set; }
        /// <summary>true = fehlende Lebensmittel werden ignoriert (keine Hunger-/Unity-Strafe).</summary>
        public bool NoFoodNeeded { get; private set; }

        public SandboxCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<ElectricityManager>(out _electricity);
            _resolver.TryResolve<WorkersManager>(out _workers);
            _resolver.TryResolve<ComputingManager>(out _computing);
            _resolver.TryResolve<UpointsManager>(out _upoints);
            _resolver.TryResolve<SettlementsManager>(out _settlements);
        }

        // ----------------------------------------------------------------------------------------
        // Einzelschalter
        // ----------------------------------------------------------------------------------------

        /// <summary>Ignoriert Strommangel (Maschinen laufen ohne ausreichenden Strom weiter).</summary>
        public void SetNoPowerNeeded(bool enabled)
        {
            if (CallIgnore(_electricity, "Cheat_IgnoreMissingPower", enabled)) NoPowerNeeded = enabled;
        }

        /// <summary>Ignoriert fehlende Arbeiter.</summary>
        public void SetNoWorkersNeeded(bool enabled)
        {
            if (CallIgnore(_workers, "Cheat_IgnoreMissingWorkers", enabled)) NoWorkersNeeded = enabled;
        }

        /// <summary>Ignoriert fehlendes Computing.</summary>
        public void SetNoComputingNeeded(bool enabled)
        {
            if (CallIgnore(_computing, "Cheat_IgnoreMissingComputing", enabled)) NoComputingNeeded = enabled;
        }

        /// <summary>Ignoriert fehlende Unity.</summary>
        public void SetNoUnityNeeded(bool enabled)
        {
            if (CallIgnore(_upoints, "Cheat_IgnoreMissingUnity", enabled)) NoUnityNeeded = enabled;
        }

        /// <summary>Ignoriert fehlende Lebensmittel (keine Hunger-/Starvation-Strafe).</summary>
        public void SetNoFoodNeeded(bool enabled)
        {
            if (CallIgnore(_settlements, "Cheat_IgnoreMissingFood", enabled)) NoFoodNeeded = enabled;
        }

        // ----------------------------------------------------------------------------------------
        // Master-Schalter: setzt alle hier verwalteten Flags auf einmal.
        // Sofortbau / Treibstoff / Wartung liegen in anderen Providern und werden vom Tab mitgesetzt.
        // ----------------------------------------------------------------------------------------

        /// <summary>Setzt alle "Ignore-Missing"-Flags dieses Providers auf <paramref name="enabled"/>.</summary>
        public void SetAllIgnoreMissing(bool enabled)
        {
            SetNoPowerNeeded(enabled);
            SetNoWorkersNeeded(enabled);
            SetNoComputingNeeded(enabled);
            SetNoUnityNeeded(enabled);
            SetNoFoodNeeded(enabled);
            Log.Info($"[{CompanySupplier.ModName}] Sandbox: alle Ignore-Missing-Flags = {enabled}.");
        }

        // ----------------------------------------------------------------------------------------
        // Reflection-Hilfe (die Cheat_IgnoreMissingX-Methoden sind internal im Game-Assembly)
        // ----------------------------------------------------------------------------------------

        /// <summary>Ruft <paramref name="method"/>(<paramref name="value"/>) auf <paramref name="target"/>
        /// per Reflection. Liefert true bei Erfolg (fuer das Status-Mirroring der UI).</summary>
        private static bool CallIgnore(object target, string method, bool value)
        {
            if (target == null) return false;
            try
            {
                var mi = target.GetType().GetMethod(method,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null, types: new[] { typeof(bool) }, modifiers: null);
                if (mi == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] {method} auf {target.GetType().Name} nicht gefunden (API-Drift?).");
                    return false;
                }
                mi.Invoke(target, new object[] { value });
                Log.Info($"[{CompanySupplier.ModName}] Sandbox: {method}({value}).");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] {method} fehlgeschlagen: {ex.Message}");
                return false;
            }
        }
    }
}
