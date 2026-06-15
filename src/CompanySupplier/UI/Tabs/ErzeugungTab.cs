using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

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

        public ErzeugungTab()
        {
            _content = BuildContent();
        }

        public string Name => "Erzeugung";

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
                CheatWidgets.SectionTitle("Gratis-Strom (KW) pro Tick"),
                BuildSliderStepperRow(
                    100000f, "KW", new[] { 1, 100, 1000, 100000 },
                    () => _powerKw,
                    v => { _powerKw = v; CheatService.Instance?.Generation?.SetFreeElectricityPerTick(v); }),

                CheatWidgets.SectionTitle("Gratis-Rechenleistung (TFlops) pro Tick"),
                BuildSliderStepperRow(
                    10000f, "TFlops", new[] { 1, 25, 100, 1000 },
                    () => _computingTFlops,
                    v => { _computingTFlops = v; CheatService.Instance?.Generation?.SetFreeComputingPerTick(v); }),

                CheatWidgets.SectionTitle("Gratis-Unity pro Monat"),
                BuildSliderStepperRow(
                    1000f, "Unity", new[] { 1, 5, 10, 25, 100 },
                    () => _unityPerMonth,
                    v => { _unityPerMonth = v; CheatService.Instance?.Generation?.SetUnityPerMonth(v); }),
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        /// <summary>
        /// Baut eine Stepper-Zeile mit Pflicht-Werte-Label: links die −Buttons (rot, absteigend), in der
        /// Mitte das Werte-Label, rechts die +Buttons (gruen, aufsteigend). Jeder Klick verschiebt den
        /// lokalen Wert um ±Stufe, begrenzt ihn bei 0 nach unten, schreibt das Label fort und setzt den
        /// neuen ABSOLUTEN Zielwert ueber <paramref name="setValue"/> ins Backend.
        /// </summary>
        // E1–E3: Slider (grob/schnell) + Werte-Label + Stepper (praezise) — alle synchron auf denselben Wert.
        private static UiComponent BuildSliderStepperRow(float max, string unit, int[] steps, Func<int> getValue, Action<int> setValue)
        {
            Slider slider = null;

            // Setzt den ABSOLUTEN Zielwert (geklemmt 0..max); der Slider zeigt ihn via ValueFormatter live.
            void ApplyAbsolute(int target)
            {
                if (target < 0) target = 0;             // E1–E3: nie negativ
                if (target > max) target = (int)max;
                slider?.Value(target, notify: false);   // Slider nachziehen (ohne Re-Trigger)
                setValue(target);                       // absoluten Zielwert ins Backend
            }

            slider = new Slider().Range(0f, max).Value(getValue(), notify: false).Label(new LocStrFormatted("Wert"))
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

            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();
            col.SetChildren(slider, stepperRow);
            return col;
        }
    }
}
