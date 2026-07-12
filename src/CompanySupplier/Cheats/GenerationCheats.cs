using System;
using System.Reflection;
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
        private ISimLoopEvents _simLoop;      // Per-Tick-Hook fuer die kosmetischen Fake-Verbrauchszahlen

        // Letzte gesetzte Werte — fuer echtes "Set" auf reine Add-APIs (Delta-Verfahren).
        private int _lastFreeComputingTFlops;

        // Monatliche Unity-Gutschrift, die das NewMonth-Abo ausschuettet.
        private Upoints _unityPerMonth = Upoints.Zero;

        // Kosmetische Fake-Verbrauchszahlen (kein Spielnutzen — ueberschreiben nur die angezeigten Werte).
        private int _fakePowerConsumptionMw;
        private int _fakeComputingConsumptionTFlops;

        // Die ThisTick-Verbrauchs-Properties haben non-public Setter (0.8.5.0) -> per Reflection setzen.
        // MethodInfo einmalig aufloesen und cachen (OnUpdateEndForUi laeuft pro Tick).
        private MethodInfo _setElecConsumed, _setElecDemanded, _setCompDemanded;
        private bool _fakeSettersResolved;

        /// <summary>Zuletzt gesetzter Gratis-Strom-Wert in kW (fuer die UI-Spiegelung).</summary>
        public int FreeElectricityKw { get; private set; }

        /// <summary>Zuletzt gesetzter Gratis-Computing-Wert in TFlops (fuer die UI-Spiegelung).</summary>
        public int FreeComputingTFlops => _lastFreeComputingTFlops;

        /// <summary>Zuletzt gesetzte Unity-Gutschrift pro Monat (fuer die UI-Spiegelung).</summary>
        public int UnityPerMonthValue { get; private set; }

        /// <summary>Kosmetischer Fake-Strom-Verbrauch (MW), der pro Tick in die Anzeige geschrieben wird.</summary>
        public int FakePowerConsumptionMw => _fakePowerConsumptionMw;

        /// <summary>Kosmetischer Fake-Computing-Verbrauch (TFlops), pro Tick in die Anzeige geschrieben.</summary>
        public int FakeComputingConsumptionTFlops => _fakeComputingConsumptionTFlops;

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

            if (_resolver.TryResolve<ISimLoopEvents>(out _simLoop) && _simLoop != null)
            {
                try
                {
                    // Per-Tick-Ende (UI): hier ueberschreiben wir die angezeigten Verbrauchszahlen, weil das
                    // Spiel sie im Tick-Anfang neu berechnet. NonSaveable -> nicht im Spielstand.
                    _simLoop.UpdateEndForUi.AddNonSaveable(this, OnUpdateEndForUi);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[{CompanySupplier.ModName}] GenerationCheats: UpdateEndForUi-Abo fehlgeschlagen: {ex.Message}");
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
                FreeElectricityKw = kw;
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
                UnityPerMonthValue = amount > 0 ? amount : 0;
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

        // ----------------------------------------------------------------------------------------
        // Kosmetische Fake-Verbrauchszahlen (Cheat++-Paritaet: "Fake Power/Computing Consumption")
        // ----------------------------------------------------------------------------------------
        // Reiner Anzeige-Cheat OHNE Spielnutzen (echten unbegrenzten Strom/Computing gibt es ueber die
        // Gratis-Erzeugung oben). Die ThisTick-Verbrauchswerte werden pro Tick-Ende ueberschrieben, weil
        // das Spiel sie im Tick-Anfang neu setzt. 0 schaltet den jeweiligen Fake ab.

        /// <summary>Setzt den angezeigten Strom-Verbrauch (MW) auf einen festen Fake-Wert (0 = aus).</summary>
        public void SetFakePowerConsumption(int mw)
        {
            _fakePowerConsumptionMw = mw < 0 ? 0 : mw;
            Log.Info($"[{CompanySupplier.ModName}] Fake-Strom-Verbrauch = {_fakePowerConsumptionMw} MW.");
        }

        /// <summary>Setzt den angezeigten Computing-Verbrauch (TFlops) auf einen festen Fake-Wert (0 = aus).</summary>
        public void SetFakeComputingConsumption(int tflops)
        {
            _fakeComputingConsumptionTFlops = tflops < 0 ? 0 : tflops;
            Log.Info($"[{CompanySupplier.ModName}] Fake-Computing-Verbrauch = {_fakeComputingConsumptionTFlops} TFlops.");
        }

        private void OnUpdateEndForUi()
        {
            if (_fakePowerConsumptionMw <= 0 && _fakeComputingConsumptionTFlops <= 0) return;
            ResolveFakeSetters();
            try
            {
                if (_electricity != null && _fakePowerConsumptionMw > 0)
                {
                    object e = Electricity.FromMw(_fakePowerConsumptionMw);
                    _setElecConsumed?.Invoke(_electricity, new[] { e });
                    _setElecDemanded?.Invoke(_electricity, new[] { e });
                }
                if (_computing != null && _fakeComputingConsumptionTFlops > 0)
                {
                    object c = Computing.FromTFlops(_fakeComputingConsumptionTFlops);
                    _setCompDemanded?.Invoke(_computing, new[] { c });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] OnUpdateEndForUi (Fake-Verbrauch): {ex.Message}");
            }
        }

        private void ResolveFakeSetters()
        {
            if (_fakeSettersResolved) return;
            _fakeSettersResolved = true;
            _setElecConsumed = GetSetter(_electricity, "ConsumedThisTick");
            _setElecDemanded = GetSetter(_electricity, "DemandedThisTick");
            _setCompDemanded = GetSetter(_computing, "DemandedThisTick");
        }

        private static MethodInfo GetSetter(object target, string property)
        {
            if (target == null) return null;
            var pi = target.GetType().GetProperty(property,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var setter = pi?.GetSetMethod(nonPublic: true);
            if (setter == null)
                Log.Warning($"[{CompanySupplier.ModName}] Setter fuer {property} nicht gefunden (Fake-Verbrauch).");
            return setter;
        }
    }
}
