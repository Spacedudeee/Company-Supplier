using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Erzeugung" (ui-spec E1–E3): dauerhafte Gratis-Erzeugung pro Tick/Monat.
    /// Drei Stepper-Zeilen mit PFLICHT-Werte-Label; der Wert wird lokal gehalten, bei jedem
    /// ±Klick neu gesetzt, nach unten auf 0 begrenzt und als ABSOLUTER Zielwert an das Backend gereicht
    /// (die Generation-Setter erwarten den Zielwert, kein Delta):
    /// - E1 Gratis-Strom (KW) pro Tick   ±1/±100/±1000/±100000 -> Generation.SetFreeElectricityPerTick(int)
    /// - E2 Gratis-Rechenleistung (TFlops) ±1/±25/±100/±1000   -> Generation.SetFreeComputingPerTick(int)
    /// - E3 Gratis-Unity pro Monat       ±1/±5/±10/±25/±100     -> Generation.SetUnityPerMonth(int)
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class ErzeugungTab : ICheatTab
    {
        private readonly UiComponent _content;

        // E1–E3: aktuell eingestellte Dauerwerte (Default 0, nie negativ).
        private int _powerKw;
        private int _computingTFlops;
        private int _unityPerMonth;

        // Sync-Aktionen der drei Zeilen (Slider + lokaler Wert aus dem Backend nachziehen).
        private readonly List<Action> _syncActions = new List<Action>();

        public ErzeugungTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(ErzeugungTab), SyncFromState);
        }

        /// <summary>Zieht Slider + lokale Werte aus dem Backend nach (via CheatUiSync) — z. B. nachdem
        /// Panik-Aus die Dauer-Erzeugung auf 0 gesetzt hat.</summary>
        private void SyncFromState()
        {
            foreach (var sync in _syncActions) sync();
        }

        public string Name => L.Tab_Erzeugung;

        // "Power"-Toolbar-Icon (Erzeugung). Verifizierter Const-Pfad aus Mafi.Base.IconsPaths
        // (ToolbarElectricity → .../Toolbar/Power.svg). String-Pfad ist in 0.8.5.0 die robuste Variante
        // (kein IconStyle mehr); fehlt das Asset, rendert der Tab trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Power.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Erz_TitlePower),
                BuildSliderStepperRow(
                    100000f, "KW", new[] { 1, 100, 1000, 100000 },
                    () => _powerKw,
                    v => { _powerKw = v; CheatService.Instance?.Generation?.SetFreeElectricityPerTick(v); },
                    v => _powerKw = v,
                    () => CheatService.Instance?.Generation?.FreeElectricityKw ?? 0),

                CheatWidgets.SectionTitle(L.Erz_TitleComputing),
                BuildSliderStepperRow(
                    10000f, "TFlops", new[] { 1, 25, 100, 1000 },
                    () => _computingTFlops,
                    v => { _computingTFlops = v; CheatService.Instance?.Generation?.SetFreeComputingPerTick(v); },
                    v => _computingTFlops = v,
                    () => CheatService.Instance?.Generation?.FreeComputingTFlops ?? 0),

                CheatWidgets.SectionTitle(L.Erz_TitleUnity),
                BuildSliderStepperRow(
                    1000f, "Unity", new[] { 1, 5, 10, 25, 100 },
                    () => _unityPerMonth,
                    v => { _unityPerMonth = v; CheatService.Instance?.Generation?.SetUnityPerMonth(v); },
                    v => _unityPerMonth = v,
                    () => CheatService.Instance?.Generation?.UnityPerMonthValue ?? 0),

                // Kosmetischer Fake-Verbrauch: reine Anzeigewerte, kein UI-Sync noetig.
                CheatWidgets.SectionTitle(L.Erz_TitleFake),
                CheatWidgets.NewIntInputRow(L.Erz_FakePower,
                    v => CheatService.Instance?.Generation?.SetFakePowerConsumption(v), min: 0),
                CheatWidgets.NewIntInputRow(L.Erz_FakeComputing,
                    v => CheatService.Instance?.Generation?.SetFakeComputingConsumption(v), min: 0),
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        /// <summary>
        /// Baut eine Stepper-Zeile mit Pflicht-Werte-Label: links die −Buttons (rot, absteigend), in der
        /// Mitte das Werte-Label, rechts die +Buttons (gruen, aufsteigend). Jeder Klick verschiebt den
        /// lokalen Wert um ±Stufe, begrenzt ihn bei 0 nach unten, schreibt das Label fort und setzt den
        /// neuen ABSOLUTEN Zielwert ueber <paramref name="setValue"/> ins Backend.
        /// <paramref name="setLocal"/>/<paramref name="readBackend"/> dienen dem zentralen UI-Sync:
        /// damit zieht die Zeile ihren Wert aus dem Backend nach, OHNE ihn erneut dorthin zu schreiben.
        /// </summary>
        // E1–E3: Slider (grob/schnell) + Werte-Label + Stepper (praezise) — alle synchron auf denselben Wert.
        private UiComponent BuildSliderStepperRow(float max, string unit, int[] steps, Func<int> getValue, Action<int> setValue,
                                                  Action<int> setLocal, Func<int> readBackend)
        {
            Slider slider = null;

            // Konkrete Wertanzeige ("12000 KW") ueber dem Regler — der Regler selbst zeigt nur seine
            // Position in Prozent, was den tatsaechlichen Wert verdeckte.
            var valueLabel = new Label(new LocStrFormatted(getValue() + " " + unit));
            void RefreshValue(int v) { if (valueLabel is IComponentWithText t) t.SetValue(new LocStrFormatted(v + " " + unit)); }

            // Setzt den ABSOLUTEN Zielwert (geklemmt 0..max).
            void ApplyAbsolute(int target)
            {
                if (target < 0) target = 0;             // E1–E3: nie negativ
                if (target > max) target = (int)max;
                slider?.Value(target, notify: false);   // Slider nachziehen (ohne Re-Trigger)
                setValue(target);                       // absoluten Zielwert ins Backend
                RefreshValue(target);                   // konkrete Anzeige nachziehen
            }

            slider = new Slider().Range(0f, max).Value(getValue(), notify: false).Label(LocStrFormatted.Empty)
                .ValueFormatter(CheatWidgets.UnitFormatter(0f, max, unit));
            slider.OnValueChanged((OnSliderValueChanged)((oldValue, newValue) => ApplyAbsolute((int)Math.Round(newValue))));
            slider.FlexGrow(1f);

            // Stepper darunter: −Buttons (rot, absteigend) … +Buttons (gruen, aufsteigend).
            var stepperRow = new Row((Px)CheatWidgets.Gap);
            var children = new List<UiComponent>();
            for (int i = steps.Length - 1; i >= 0; i--)
            {
                int step = steps[i];
                children.Add(new ButtonText(Button.Danger, new LocStrFormatted($"-{step}"), () => ApplyAbsolute(getValue() - step)));
            }
            foreach (int step in steps)
            {
                int s = step;
                children.Add(new ButtonText(Button.Primary, new LocStrFormatted($"+{s}"), () => ApplyAbsolute(getValue() + s)));
            }
            stepperRow.SetChildren(children.ToArray());

            // Sync-Aktion registrieren: lokalen Wert + Slider aus dem Backend nachziehen (kein Backend-Write).
            _syncActions.Add(() =>
            {
                int backend = readBackend();
                if (backend < 0) backend = 0;
                setLocal(backend);
                slider?.Value(backend, notify: false);
                RefreshValue(backend);
            });

            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();
            col.SetChildren(valueLabel, slider, stepperRow);
            return col;
        }
    }
}
