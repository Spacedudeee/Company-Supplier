using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;

namespace CompanySupplier.UI
{
    /// <summary>
    /// UiToolkit-Hilfen fuer das Cheat-Menue — analog zu <c>UIBuilderExtensions</c> der Referenz, aber
    /// gegen das 0.8.5.0-Framework (<see cref="Button"/>/<see cref="Row"/>/<see cref="Toggle"/> direkt
    /// instanziiert, Styling ueber Extension-Methoden). Liefert die wiederkehrenden Bausteine:
    /// Inkrement-Gruppe (−Danger/+Primary), Toggle-Zeile, Primary-/Danger-Buttons.
    /// </summary>
    internal static class CheatWidgets
    {
        /// <summary>Standard-Abstand zwischen Kindelementen in Zeilen/Spalten (px).</summary>
        public const int Gap = 8;

        /// <summary>
        /// Baut eine Inkrement-Button-Gruppe: pro Eintrag ein roter <c>-N</c>-Button (links) und ein gruener
        /// <c>+N</c>-Button (rechts). Klick ruft die hinterlegte Aktion mit <c>±N</c> auf.
        /// Reihenfolge wie in der Referenz: erst alle negativen (absteigend), dann alle positiven (aufsteigend).
        /// </summary>
        public static Row NewIncrementButtonGroup(IReadOnlyDictionary<int, Action<int>> incrementsAndActions)
        {
            var row = new Row((Px)Gap);
            var children = new List<UiComponent>();

            // Negative Inkremente (rot, links). Groesster Betrag zuerst -> "-50 -25 -5".
            var steps = new List<int>(incrementsAndActions.Keys);
            steps.Sort();
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                int step = steps[i];
                Action<int> action = incrementsAndActions[step];
                children.Add(new ButtonText(Button.Danger, new LocStrFormatted($"-{step}"), () => action(-step)));
            }

            // Positive Inkremente (gruen, rechts). Kleinster Betrag zuerst -> "+5 +25 +50".
            foreach (int step in steps)
            {
                Action<int> action = incrementsAndActions[step];
                children.Add(new ButtonText(Button.Primary, new LocStrFormatted($"+{step}"), () => action(step)));
            }

            row.SetChildren(children.ToArray());
            return row;
        }

        /// <summary>
        /// Baut eine Toggle-Zeile mit Beschriftung. <paramref name="initial"/> setzt den Startzustand,
        /// <paramref name="onChanged"/> wird bei jedem Umschalten aufgerufen. Optionaler Tooltip.
        /// Der Status laesst sich spaeter ueber <c>toggle.Value(bool)</c> aus dem Spielzustand zurueckschreiben.
        /// </summary>
        public static Toggle NewToggleRow(string label, bool initial, Action<bool> onChanged, string tooltip = null)
        {
            var toggle = new Toggle(standalone: true);
            // .Label(...) ist die generische Text-Extension (UiComponentWithTextExtensions) — vermeidet den
            // Cast auf das explizit implementierte IComponentWithLabel.SetLabel.
            toggle.Label(new LocStrFormatted(label));
            toggle.Value(initial);
            toggle.OnValueChanged(v => onChanged(v));
            if (!string.IsNullOrEmpty(tooltip))
                toggle.Tooltip(new LocStrFormatted(tooltip), enabled: true, isError: false, openBelow: false);
            return toggle;
        }

        /// <summary>Gruener (Primary) Aktions-Button.</summary>
        public static ButtonText PrimaryButton(string label, Action onClick, string tooltip = null)
        {
            var btn = new ButtonText(Button.Primary, new LocStrFormatted(label), onClick);
            if (!string.IsNullOrEmpty(tooltip))
                btn.Tooltip(new LocStrFormatted(tooltip), enabled: true, isError: false, openBelow: false);
            return btn;
        }

        /// <summary>Roter (Danger) Aktions-Button.</summary>
        public static ButtonText DangerButton(string label, Action onClick, string tooltip = null)
        {
            var btn = new ButtonText(Button.Danger, new LocStrFormatted(label), onClick);
            if (!string.IsNullOrEmpty(tooltip))
                btn.Tooltip(new LocStrFormatted(tooltip), enabled: true, isError: false, openBelow: false);
            return btn;
        }

        /// <summary>Neutraler (General) Aktions-Button.</summary>
        public static ButtonText GeneralButton(string label, Action onClick, string tooltip = null)
        {
            var btn = new ButtonText(Button.General, new LocStrFormatted(label), onClick);
            if (!string.IsNullOrEmpty(tooltip))
                btn.Tooltip(new LocStrFormatted(tooltip), enabled: true, isError: false, openBelow: false);
            return btn;
        }

        /// <summary>Section-Titel (ersetzt das alte <c>AddSectionTitle</c>).</summary>
        public static Title SectionTitle(string text) => new Title(new LocStrFormatted(text));

        /// <summary>
        /// Slider-ValueFormatter: zeigt den (intern als Prozent der Range gefuehrten) Wert als ABSOLUTE Zahl
        /// in der Einheit — live beim Ziehen (statt "%" und statt erst beim Loslassen).
        /// absoluterWert = min + prozent.Apply(max - min).
        /// </summary>
        public static Option<Func<Percent, LocStrFormatted>> UnitFormatter(float min, float max, string unit)
        {
            int baseMin = (int)min;
            int span = (int)(max - min);
            Func<Percent, LocStrFormatted> fmt = p =>
            {
                int val = baseMin + p.Apply(span);
                return new LocStrFormatted(string.IsNullOrEmpty(unit) ? val.ToString() : (val + " " + unit));
            };
            return Option.Some(fmt);
        }

        // ------------------------------------------------------------------------------------------
        // Proto-Anzeigenamen (lokalisiert / Deutsch)
        // ------------------------------------------------------------------------------------------

        // Fuegt ein Leerzeichen zwischen einem Buchstaben und einer direkt folgenden Ziffer ein
        // ("Eisen2" -> "Eisen 2"). Die deutschen Spielnamen nutzen roemische Ziffern mit Leerzeichen
        // ("Konstruktionsteile II"), daher greift das praktisch nur beim Id-Fallback. \p{L} deckt Umlaute ab.
        private static readonly Regex _letterBeforeDigit =
            new Regex(@"(?<=\p{L})(?=\d)", RegexOptions.Compiled);

        /// <summary>
        /// Lokalisierter Anzeigename eines Protos (Produkt/Material/…): der vom Spiel uebersetzte Name
        /// (<c>proto.Strings.Name.TranslatedString</c>, z. B. "Eisenerz" / "Konstruktionsteile II"). Faellt auf
        /// die Id ohne "Product_"-Praefix zurueck, falls keine Uebersetzung vorliegt. Setzt in beiden Faellen
        /// ein Leerzeichen vor direkt angeklebte Ziffern.
        /// </summary>
        public static string ProtoDisplayName(Mafi.Core.Prototypes.Proto proto)
        {
            if (proto == null) return string.Empty;

            string name = proto.Strings.Name.TranslatedString;
            if (string.IsNullOrEmpty(name))
            {
                string id = proto.Id.ToString();
                const string prefix = "Product_";
                if (id.StartsWith(prefix, StringComparison.Ordinal))
                    id = id.Substring(prefix.Length);
                name = id;
            }
            return _letterBeforeDigit.Replace(name, " ");
        }

        /// <summary>Lokalisierter Anzeigename als <see cref="LocStrFormatted"/> (fuer Dropdown-Option-Labels).</summary>
        public static LocStrFormatted ProtoDisplayLabel(Mafi.Core.Prototypes.Proto proto)
            => new LocStrFormatted(ProtoDisplayName(proto));
    }
}
