using System;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Settlements;
using Mafi.Core.Population;
using Mafi.Core.Simulation;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider fuer die Gruppe "Bevoelkerung / Siedlung".
    ///
    /// Deckt ab:
    ///  - AddPopulation(int)              : Siedlungs-Bevoelkerung hinzufuegen/entfernen.
    ///  - AddUnity(int)                   : Unity-Punkte gutschreiben.
    ///  - SetDiseasesDisabled(bool)       : neu auftretende Seuchen automatisch beenden.
    ///  - SetMaxConsumptionHappiness(bool): Versorgungs-/Lebensmittel-Zufriedenheit dauerhaft maximal.
    ///
    /// Robustheit (Early-Access-API kann driften): jeder Manager wird mit TryResolve geholt und jeder
    /// Cheat in try/catch gekapselt; ein gebrochener Cheat darf weder Mod noch Spiel zum Absturz bringen.
    /// </summary>
    public sealed class PopulationCheats
    {
        private readonly DependencyResolver _resolver;

        // Verifiziert gegen 0.8.5.0:
        private SettlementsManager _settlements;   // Mafi.Core.Buildings.Settlements — AddPops/RemovePopsAsMuchAs
        private UpointsManager _upoints;           // Mafi.Core.Population — GenerateUnity
        private PopsHealthManager _popsHealth;     // Mafi.Core.Population — DisableDiseases / CurrentDisease
        private WorkersManager _workers;           // Mafi.Core.Population — Cheat_addWorkers (freie Arbeiter)
        private ICalendar _calendar;               // Mafi.Core.Simulation — NewDay-Event

        /// <summary>true = neu auftretende Seuchen werden taeglich automatisch beendet.</summary>
        public bool DiseasesDisabled { get; private set; }

        /// <summary>true = Versorgungs-/Lebensmittel-Zufriedenheit wird taeglich auf Maximum gehalten.</summary>
        public bool MaxConsumptionHappiness { get; private set; }

        /// <summary>true = die Unity-Punkte werden taeglich bis zur Kapazitaet aufgefuellt.</summary>
        public bool KeepUnityFull { get; private set; }

        /// <summary>true = Siedlungs-Hausmuell (Landfill je Einwohner) wird taeglich auf 0 gehalten.</summary>
        public bool NoMunicipalWaste { get; private set; }

        /// <summary>true = Siedlungs-Biomuell wird taeglich vollstaendig reduziert (Reduktion = 100%).</summary>
        public bool NoBiowaste { get; private set; }

        public PopulationCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<SettlementsManager>(out _settlements);
            _resolver.TryResolve<UpointsManager>(out _upoints);
            _resolver.TryResolve<PopsHealthManager>(out _popsHealth);
            _resolver.TryResolve<WorkersManager>(out _workers);
            _resolver.TryResolve<ICalendar>(out _calendar);

            // Taeglicher Hook: beendet aktive Seuchen bzw. haelt die Zufriedenheit oben, solange aktiv.
            // AddNonSaveable -> wird NICHT im Spielstand persistiert (reiner Laufzeit-Cheat).
            if (_calendar != null)
            {
                try { _calendar.NewDay.AddNonSaveable(this, OnNewDay); }
                catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] PopulationCheats NewDay-Hook fehlgeschlagen: {ex.Message}"); }
            }
        }

        // ----------------------------------------------------------------------------------------
        // A5: Bevoelkerung hinzufuegen / entfernen
        // ----------------------------------------------------------------------------------------

        /// <summary>Fuegt <paramref name="delta"/> Bevoelkerung hinzu (positiv) oder entfernt sie (negativ).</summary>
        public void AddPopulation(int delta)
        {
            if (_settlements == null || delta == 0) return;
            try
            {
                if (delta > 0)
                {
                    // PopsAdditionReason.Other = neutraler Cheat-Zuzug (kein Refugee-/Adopt-Bonus).
                    _settlements.AddPops(delta, PopsAdditionReason.Other);
                }
                else
                {
                    _settlements.RemovePopsAsMuchAs(Math.Abs(delta));
                }
                Log.Info($"[{CompanySupplier.ModName}] Bevoelkerung {(delta > 0 ? "+" : "")}{delta}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] AddPopulation: {ex.Message}");
            }
        }

        /// <summary>Setzt die Gesamt-Bevoelkerung ABSOLUT auf <paramref name="target"/> (&gt;= 0). Das Spiel bietet
        /// keinen Absolut-Setter, daher wird das Delta zum aktuellen Stand ueber den bestehenden Add/Remove-Pfad
        /// angewandt. Nach unten kann der Wert das Siedlungs-Minimum nicht unterschreiten (RemovePopsAsMuchAs).</summary>
        public void SetPopulation(int target)
        {
            if (_settlements == null) return;
            if (target < 0) target = 0;
            try
            {
                int current = _settlements.GetTotalPopulation();
                AddPopulation(target - current);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetPopulation({target}): {ex.Message}");
            }
        }

        /// <summary>Aktuelle Gesamt-Bevoelkerung (fuer die UI-Anzeige), oder -1 wenn der Manager fehlt.</summary>
        public int GetTotalPopulation()
        {
            try { return _settlements?.GetTotalPopulation() ?? -1; }
            catch { return -1; }
        }

        /// <summary>Fuegt <paramref name="count"/> freie Arbeiter direkt in den Arbeitskraeftepool ein — ohne
        /// Wohnraum/Bevoelkerung. Nutzt die public Cheat-API <c>WorkersManager.Cheat_addWorkers</c> (0.8.5.0).</summary>
        public void AddWorkers(int count)
        {
            if (_workers == null || count == 0) return;
            try
            {
                _workers.Cheat_addWorkers(count);
                Log.Info($"[{CompanySupplier.ModName}] {(count > 0 ? "+" : "")}{count} freie Arbeiter.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] AddWorkers({count}): {ex.Message}");
            }
        }

        /// <summary>Aktuell freie Arbeiter (fuer die UI-Anzeige), oder -1 wenn der Manager fehlt.</summary>
        public int GetFreeWorkers()
        {
            try { return _workers?.AmountOfFreeWorkers ?? -1; }
            catch { return -1; }
        }

        // ----------------------------------------------------------------------------------------
        // A8: Unity hinzufuegen
        // ----------------------------------------------------------------------------------------

        /// <summary>Schreibt <paramref name="amount"/> Unity-Punkte ueber die FreeUnity-Kategorie gut.</summary>
        public void AddUnity(int amount)
        {
            if (_upoints == null || amount == 0) return;
            try
            {
                // 0.8.5.0: public GenerateUnity(ID category, Upoints generated, Nullable<Upoints> max,
                //          Nullable<Upoints> possibleMax, Nullable<LocStrFormatted> extraTitle).
                // Die Nullable-Parameter mit null lassen (Default-Verhalten, keine Deckelung).
                _upoints.GenerateUnity(IdsCore.UpointsCategories.FreeUnity, new Upoints(amount), null, null, null);
                Log.Info($"[{CompanySupplier.ModName}] +{amount} Unity gutgeschrieben.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] AddUnity: {ex.Message}");
            }
        }

        /// <summary>Schaltet "Unity voll halten" ein/aus. Bei "an" wird sofort einmal aufgefuellt; der
        /// taegliche Hook (OnNewDay) haelt es danach voll.</summary>
        public void SetKeepUnityFull(bool enabled)
        {
            KeepUnityFull = enabled;
            if (enabled) TopUpUnity();
            Log.Info($"[{CompanySupplier.ModName}] Unity voll halten = {enabled}.");
        }

        /// <summary>Fuellt die Unity-Punkte bis zur Kapazitaet auf. Da im Spiel gespeicherte Unity nicht ueber
        /// <c>TotalUnityCap</c> hinaus geht (Ueberschuss verfaellt), genuegt es, die volle Cap-Menge ueber die
        /// FreeUnity-Kategorie zu generieren — der Stand steht danach am Cap.</summary>
        private void TopUpUnity()
        {
            if (_upoints == null) return;
            try
            {
                Upoints cap = _upoints.TotalUnityCap;
                _upoints.GenerateUnity(IdsCore.UpointsCategories.FreeUnity, cap, cap, cap, null);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] TopUpUnity: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // A3: Krankheiten deaktivieren
        // ----------------------------------------------------------------------------------------

        /// <summary>Schaltet die Seuchen-Unterdrueckung ein/aus. Bei "an" wird zusaetzlich die bereits
        /// laufende Seuche sofort beendet (und der taegliche Hook beendet kuenftige Seuchen).</summary>
        public void SetDiseasesDisabled(bool disabled)
        {
            if (_popsHealth == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetDiseasesDisabled: PopsHealthManager nicht aufgeloest.");
                return;
            }
            try
            {
                // 0.8.5.0: Der Setter von DisableDiseases ist NICHT public (verifiziert -> CS0200);
                // gesetzt wird ueber die internal Methode SetDisableDiseases(bool) per Reflection.
                // Das Mirror-Flag erst NACH erfolgreichem Backend-Aufruf setzen — sonst luegen
                // UI-Spiegelung und gespeicherter Zustand, wenn die Reflection fehlschlaegt.
                if (!CallNonPublic(_popsHealth, "SetDisableDiseases", disabled)) return;
                DiseasesDisabled = disabled;
                if (disabled) EndActiveDisease();
                Log.Info($"[{CompanySupplier.ModName}] Krankheiten deaktiviert = {disabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetDiseasesDisabled: {ex.Message}");
            }
        }

        /// <summary>Beendet die aktuell laufende Seuche, falls eine aktiv ist. Der Getter von
        /// CurrentDisease ist public, die Setter von CurrentDisease/CurrentDiseaseMonthsLeft sind in
        /// 0.8.5.0 NICHT public (verifiziert -> CS0200) und werden per Reflection "weggesetzt".</summary>
        private void EndActiveDisease()
        {
            if (_popsHealth == null) return;
            try
            {
                if (_popsHealth.CurrentDisease != Option<DiseaseProto>.None)
                {
                    SetNonPublicProperty(_popsHealth, "CurrentDisease", Option<DiseaseProto>.None);
                    SetNonPublicProperty(_popsHealth, "CurrentDiseaseMonthsLeft", 0);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] EndActiveDisease: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // A4: Lebensmittel-/Versorgungs-Zufriedenheit maximal
        // ----------------------------------------------------------------------------------------

        /// <summary>Haelt die Siedlungs-Zufriedenheit aus Versorgung/Lebensmitteln dauerhaft auf Maximum.
        ///
        /// Umsetzung (0.8.5.0, reflection-frei ueber public Setter):
        ///  (1) SettlementsManager.IgnoreMissingFood = true  -> keine Hunger-/Starvation-Strafe, keine
        ///      Unity-Penalty fuer fehlende Lebensmittel (public { get; set; } in 0.8.5.0).
        ///  (2) Taeglich (OnNewDay, NACH der Spiel-Neuberechnung) wird jede PopNeed jeder Siedlung auf
        ///      volle Befriedigung getoppt: PercentSatisfiedLastMonth=100%, UnityAfterLastUpdate=Max.
        ///      Da das Spiel die Werte jeden Tick neu berechnet, ist das ein laufendes Ueberschreiben.
        /// </summary>
        public void SetMaxConsumptionHappiness(bool enabled)
        {
            if (_settlements == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetMaxConsumptionHappiness: SettlementsManager nicht aufgeloest.");
                return;
            }
            try
            {
                // 0.8.5.0: Der Setter von IgnoreMissingFood ist NICHT public (verifiziert -> CS0200);
                // gesetzt ueber die internal Methode Cheat_IgnoreMissingFood(bool) per Reflection.
                //
                // WICHTIG: Dasselbe Spiel-Flag wird auch von SandboxCheats.SetNoFoodNeeded ("Keine
                // Lebensmittel noetig") gesteuert. Damit sich die beiden Toggles nicht gegenseitig
                // ueberschreiben, wird immer das ODER beider Wuensche geschrieben.
                bool sandboxWantsIt = CheatService.Instance?.Sandbox?.NoFoodNeeded ?? false;
                if (!CallNonPublic(_settlements, "Cheat_IgnoreMissingFood", enabled || sandboxWantsIt)) return;
                MaxConsumptionHappiness = enabled;          // Mirror erst nach erfolgreichem Backend-Aufruf
                if (enabled) ApplyMaxHappiness();           // sofort einmal anwenden, dann taeglich im Hook
                Log.Info($"[{CompanySupplier.ModName}] Versorgungs-Zufriedenheit max = {enabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] SetMaxConsumptionHappiness: {ex.Message}");
            }
        }

        /// <summary>Setzt fuer jede Siedlung jede PopNeed auf volle Befriedigung (public Setter auf PopNeed).</summary>
        private void ApplyMaxHappiness()
        {
            if (_settlements == null) return;
            try
            {
                foreach (var settlement in _settlements.Settlements)
                {
                    if (settlement == null) continue;
                    // Settlement.AllNeeds = ImmutableArray<PopNeed> (public Field, verifiziert 0.8.5.0).
                    foreach (var need in settlement.AllNeeds)
                    {
                        if (need == null) continue;
                        // PercentSatisfiedLastMonth-Setter ist non-public (-> CS0200); public
                        // SetPercentSatisfiedLastMonth(Percent) verwenden. Die uebrigen Setter sind public.
                        need.SetPercentSatisfiedLastMonth(Percent.Hundred);
                        need.WasNotFullySatisfiedLastDay = false;
                        // Unity-Ertrag der Need auf den maximal moeglichen Wert heben.
                        need.UnityAfterLastUpdate = need.PossibleMaxAfterLastUpdate;
                        need.MaxAfterLastUpdate = need.PossibleMaxAfterLastUpdate;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] ApplyMaxHappiness: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Siedlungs-Abfall: Hausmuell (Landfill) + Biomuell dauerhaft unterdruecken
        // ----------------------------------------------------------------------------------------

        /// <summary>Schaltet "Kein Hausmuell" ein/aus. Setzt je Siedlung <c>BaseLandfillPerPopPerDay = 0</c>
        /// (public Setter, 0.8.5.0). Wie A4 wird der Wert taeglich neu geschrieben, weil das Spiel ihn im
        /// Tages-Tick neu berechnet; nach dem Ausschalten stellt die Spiel-Neuberechnung das Original wieder her.</summary>
        public void SetNoMunicipalWaste(bool enabled)
        {
            NoMunicipalWaste = enabled;
            if (enabled) ApplyNoMunicipalWaste();
            Log.Info($"[{CompanySupplier.ModName}] Kein Hausmuell = {enabled}.");
        }

        /// <summary>Schaltet "Kein Biomuell" ein/aus. Setzt je Siedlung <c>BioWasteReductionMultiplier = 100%</c>
        /// (voller Biomuell-Abbau). Taeglich neu angewandt (siehe <see cref="SetNoMunicipalWaste"/>).</summary>
        public void SetNoBiowaste(bool enabled)
        {
            NoBiowaste = enabled;
            if (enabled) ApplyNoBiowaste();
            Log.Info($"[{CompanySupplier.ModName}] Kein Biomuell = {enabled}.");
        }

        private void ApplyNoMunicipalWaste()
        {
            if (_settlements == null) return;
            try
            {
                foreach (var settlement in _settlements.Settlements)
                {
                    if (settlement == null) continue;
                    // BaseLandfillPerPopPerDay hat einen non-public Setter (0.8.5.0) -> per Reflection setzen.
                    SetNonPublicProperty(settlement, "BaseLandfillPerPopPerDay", PartialQuantity.Zero);
                }
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ApplyNoMunicipalWaste: {ex.Message}"); }
        }

        private void ApplyNoBiowaste()
        {
            if (_settlements == null) return;
            try
            {
                foreach (var settlement in _settlements.Settlements)
                {
                    if (settlement == null) continue;
                    // BioWasteReductionMultiplier ist ein readonly Feld (0.8.5.0); 100% = voller Abbau -> Reflection.
                    SetInstanceField(settlement, "BioWasteReductionMultiplier", Percent.Hundred);
                }
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] ApplyNoBiowaste: {ex.Message}"); }
        }

        // ----------------------------------------------------------------------------------------
        // Taeglicher Tick-Hook (NewDay): erfuellt die Dauer-Toggles A3 + A4 + Abfall
        // ----------------------------------------------------------------------------------------

        private void OnNewDay()
        {
            if (DiseasesDisabled) EndActiveDisease();
            if (MaxConsumptionHappiness) ApplyMaxHappiness();
            if (KeepUnityFull) TopUpUnity();
            if (NoMunicipalWaste) ApplyNoMunicipalWaste();
            if (NoBiowaste) ApplyNoBiowaste();
        }

        // ----------------------------------------------------------------------------------------
        // Reflection-Hilfen (mehrere 0.8.5.0-Member haben non-public Setter/Methoden)
        // ----------------------------------------------------------------------------------------

        /// <summary>Ruft eine internal/private Instanzmethode per Reflection auf. Liefert true bei
        /// Erfolg — Aufrufer setzen ihre Mirror-Flags nur dann (UI-/Persistenz-Wahrheit).</summary>
        private static bool CallNonPublic(object target, string method, params object[] args)
        {
            if (target == null) return false;
            var mi = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (mi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Methode {method} nicht gefunden.");
                return false;
            }
            mi.Invoke(target, args.Length == 0 ? null : args);
            return true;
        }

        /// <summary>Setzt eine Property mit non-public Setter per Reflection.</summary>
        private static void SetNonPublicProperty(object target, string property, object value)
        {
            if (target == null) return;
            var pi = target.GetType().GetProperty(property,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var setter = pi?.GetSetMethod(nonPublic: true);
            if (setter == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Setter fuer {property} nicht gefunden.");
                return;
            }
            setter.Invoke(target, new[] { value });
        }

        /// <summary>Setzt ein (auch readonly) Instanzfeld per Reflection — funktioniert auf .NET 4.8 auch fuer
        /// readonly Klassenfelder.</summary>
        private static void SetInstanceField(object target, string field, object value)
        {
            if (target == null) return;
            var fi = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Feld {field} nicht gefunden.");
                return;
            }
            fi.SetValue(target, value);
        }
    }
}
