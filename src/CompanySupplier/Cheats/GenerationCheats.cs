using System;
using Mafi;
using Mafi.Core;
using Mafi.Core.Factory.ComputingPower;
using Mafi.Core.Factory.ElectricPower;
using Mafi.Core.Population;
using Mafi.Core.Simulation;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Gruppe "Dauer-Erzeugung": dauerhaft wirkende Cheats, die pro Tick bzw. pro Monat einen
    /// festen Betrag gratis erzeugen — Gratis-Strom, Gratis-Computing und monatliche Unity-Gutschrift.
    ///
    /// Robustheit (Early-Access-API driftet): jeder Manager wird mit TryResolve geholt und jeder
    /// Cheat in try/catch gekapselt; fehlt ein Manager, wird der jeweilige Cheat still zum No-Op.
    ///
    /// Implementiert <see cref="IEventOwner"/>, weil <c>ICalendar.NewMonth</c> (ein Mafi.IEvent) einen
    /// Owner verlangt. Das Abo wird per <c>AddNonSaveable</c> registriert, damit es NICHT in den
    /// Spielstand serialisiert wird — Cheats sollen den Save nicht verändern.
    /// </summary>
    public sealed class GenerationCheats : IEventOwner
    {
        private readonly DependencyResolver _resolver;

        private ElectricityManager _electricity;
        private ComputingManager _computing;
        private UpointsManager _upoints;
        private ICalendar _calendar;

        // Letzte gesetzte Werte — fuer echtes "Set" auf reine Add-APIs (Delta-Verfahren).
        private int _lastFreeComputingTFlops;

        // Monatliche Unity-Gutschrift, die das NewMonth-Abo ausschuettet.
        private Upoints _unityPerMonth = Upoints.Zero;

        public GenerationCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<ElectricityManager>(out _electricity);
            _resolver.TryResolve<ComputingManager>(out _computing);
            _resolver.TryResolve<UpointsManager>(out _upoints);

            if (_resolver.TryResolve<ICalendar>(out _calendar) && _calendar != null)
            {
                try
                {
                    // NonSaveable: das Abo lebt nur zur Laufzeit, landet nicht im Spielstand.
                    _calendar.NewMonth.AddNonSaveable(this, OnNewMonth);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] GenerationCheats: NewMonth-Abo fehlgeschlagen: {ex.Message}");
                }
            }
        }

        /// <summary><see cref="IEventOwner"/>: ein Cheat-Provider wird nie zerstoert, lebt so lange wie der Mod.</summary>
        public bool IsDestroyed => false;

        // ----------------------------------------------------------------------------------------
        // Cheat: dauerhafte Gratis-Stromerzeugung
        // ----------------------------------------------------------------------------------------

        /// <summary>Setzt die dauerhafte Gratis-Stromerzeugung auf <paramref name="kw"/> Kilowatt pro Tick.
        /// 0 schaltet den Cheat ab. Nutzt die public 0.8.5.0-API
        /// (<c>Cheat_ClearFreeElectricityPerTick</c> + <c>Cheat_AddFreeElectricityPerTick</c>) statt des
        /// frueheren Reflection-Hacks auf <c>m_freeElectricityPerTick</c>.</summary>
        public void SetFreeElectricityPerTick(int kw)
        {
            if (_electricity == null) return;
            try
            {
                // Erst zuruecksetzen, dann den Zielwert setzen -> echtes "Set", kein Aufsummieren.
                _electricity.Cheat_ClearFreeElectricityPerTick();
                if (kw != 0)
                    _electricity.Cheat_AddFreeElectricityPerTick(Electricity.FromKw(kw));
                Log.Info($"[{CompanySupplier.ModName}] Gratis-Strom = {kw} kW/Tick.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetFreeElectricityPerTick: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Cheat: dauerhafte Gratis-Computing-Erzeugung
        // ----------------------------------------------------------------------------------------

        /// <summary>Setzt die dauerhafte Gratis-Computing-Erzeugung auf <paramref name="tflops"/> TFlops
        /// pro Tick. 0 schaltet den Cheat ab. <c>ComputingManager</c> hat in 0.8.5.0 nur die additive
        /// public API <c>Cheat_AddFreeComputingPerTick</c> (kein Clear), daher wird ein echtes "Set" ueber
        /// das Delta zum zuletzt gesetzten Wert realisiert — solange ausschliesslich dieser Cheat das
        /// Feld anfasst, ist das Ergebnis exakt der Zielwert.</summary>
        public void SetFreeComputingPerTick(int tflops)
        {
            if (_computing == null) return;
            try
            {
                int delta = tflops - _lastFreeComputingTFlops;
                if (delta != 0)
                    _computing.Cheat_AddFreeComputingPerTick(Computing.FromTFlops(delta));
                _lastFreeComputingTFlops = tflops;
                Log.Info($"[{CompanySupplier.ModName}] Gratis-Computing = {tflops} TFlops/Tick.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetFreeComputingPerTick: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Cheat: X Unity je Monat gutschreiben
        // ----------------------------------------------------------------------------------------

        /// <summary>Schreibt ab dem naechsten Monatswechsel dauerhaft <paramref name="amount"/> Unity pro
        /// Monat gut. 0 schaltet die monatliche Gutschrift ab. Die Ausschuettung erfolgt im
        /// <c>ICalendar.NewMonth</c>-Handler ueber <c>UpointsManager.GenerateUnity</c>.</summary>
        public void SetUnityPerMonth(int amount)
        {
            try
            {
                _unityPerMonth = amount > 0 ? new Upoints(amount) : Upoints.Zero;
                Log.Info($"[{CompanySupplier.ModName}] Unity/Monat = {amount}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetUnityPerMonth: {ex.Message}");
            }
        }

        /// <summary>Wird vom Spiel bei jedem Monatswechsel aufgerufen (NewMonth-Abo) und schuettet die
        /// konfigurierte Unity-Menge in die Kategorie <c>FreeUnity</c> aus.</summary>
        private void OnNewMonth()
        {
            if (_upoints == null || _unityPerMonth.IsNotPositive) return;
            try
            {
                _upoints.GenerateUnity(IdsCore.UpointsCategories.FreeUnity, _unityPerMonth);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] OnNewMonth (Unity-Gutschrift): {ex.Message}");
            }
        }
    }
}
