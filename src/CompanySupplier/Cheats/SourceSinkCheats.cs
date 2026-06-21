using System;
using System.Reflection;
using Mafi;
using Mafi.Core;   // SourceSinkCheatManager

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Unendlich-Quelle/Senke": schaltet das eingebaute Cheat-Gebaeude
    /// <c>CheatingProductsSourceSink</c> in der Bau-Toolbar frei. Das Gebaeude erzeugt jedes Produkt aus
    /// dem Nichts (Quelle) bzw. schluckt jeden Output bodenlos (Senke) und wird vom Spieler danach ganz
    /// normal ueber die Toolbar platziert — wir muessen also nichts selbst spawnen.
    ///
    /// 0.8.5.0 (per ApiInspector verifiziert): <see cref="SourceSinkCheatManager"/> stellt das public
    /// <c>SetAreSourcesAndSinksAllowed(bool)</c> bereit; das Einblenden der Toolbar-Marker laeuft ueber das
    /// internal <c>ShowSourcesSinksInToolbar()</c> (Reflection). Einmal eingeblendete Marker bleiben i. d. R.
    /// sichtbar; das Deaktivieren verbietet wieder das Setzen/Wirken.
    ///
    /// Robustheit: Manager via TryResolve, jeder Cheat in try/catch + Log.Warning.
    /// </summary>
    public sealed class SourceSinkCheats
    {
        private readonly DependencyResolver _resolver;
        private SourceSinkCheatManager _manager;

        /// <summary>true = Quelle/Senke ist freigeschaltet (von uns gesetzt).</summary>
        public bool Enabled { get; private set; }

        public SourceSinkCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<SourceSinkCheatManager>(out _manager);
        }

        /// <summary>Schaltet die Unendlich-Quelle/Senke in der Bau-Toolbar frei (true) bzw. verbietet sie (false).</summary>
        public void SetEnabled(bool enabled)
        {
            if (_manager == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SourceSink: SourceSinkCheatManager nicht aufgeloest.");
                return;
            }
            try
            {
                _manager.SetAreSourcesAndSinksAllowed(enabled);
                if (enabled) ShowInToolbar();
                Enabled = enabled;
                Log.Info($"[{CompanySupplier.ModName}] Unendlich-Quelle/Senke freigeschaltet = {enabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SourceSink umschalten: {ex.Message}");
            }
        }

        /// <summary>Blendet die Toolbar-Marker ein (internal Methode -> Reflection).</summary>
        private void ShowInToolbar()
        {
            try
            {
                var mi = typeof(SourceSinkCheatManager).GetMethod(
                    "ShowSourcesSinksInToolbar",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (mi == null)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] SourceSink: ShowSourcesSinksInToolbar nicht gefunden (API-Drift?).");
                    return;
                }
                mi.Invoke(_manager, null);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SourceSink ShowInToolbar: {ex.Message}");
            }
        }
    }
}
