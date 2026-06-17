using System;
using System.Collections.Generic;
using System.Globalization;
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

        // ------------------------------------------------------------------------------------------
        // Zahlen-Eingabefelder (direkte Werteingabe)
        // ------------------------------------------------------------------------------------------
        // UiToolkit hat KEIN natives Eingabefeld (nur Slider/Stepper/Dropdown/Toggle). Wir nutzen daher das
        // rohe UnityEngine.UIElements.TextField und wrappen es ueber den oeffentlichen UiComponent(VisualElement)-
        // ctor in den Mafi-UI-Baum. Kein Placeholder in dieser Unity-Version -> Hinweis via Label/Vorbelegung.

        /// <summary>Gemeinsames Geruest: Beschriftung + TextField (als UiComponent) + "Setzen"-Button. Bei Enter
        /// oder Button-Klick wird <paramref name="tryApply"/> mit dem getrimmten Rohtext gerufen; liefert es
        /// <c>false</c>, meldet die zentrale Statuszeile eine ungueltige Eingabe.</summary>
        private static Row NewInputRow(string label, string hint, string setLabel, Func<string, bool> tryApply)
        {
            var caption = new Label(new LocStrFormatted(label));

            var field = new UnityEngine.UIElements.TextField { multiline = false, maxLength = 12, isDelayed = true };
            StyleInputField(field); // sichtbarer Rahmen/Hintergrund — rohes TextField ist sonst kaum erkennbar
            if (!string.IsNullOrEmpty(hint)) field.value = hint;
            var fieldComp = new UiComponent(field);

            void Commit()
            {
                string raw = (field.value ?? string.Empty).Trim();
                if (!tryApply(raw))
                    CheatMenuStatus.Show($"Ungültige Zahl: \"{raw}\"");
            }

            field.RegisterCallback<UnityEngine.UIElements.KeyDownEvent>(e =>
            {
                if (e.keyCode == UnityEngine.KeyCode.Return || e.keyCode == UnityEngine.KeyCode.KeypadEnter)
                    Commit();
            });

            var row = new Row((Px)Gap).AlignItemsCenter();
            row.SetChildren(caption, fieldComp, PrimaryButton(setLabel, Commit));
            return row;
        }

        /// <summary>Beschriftetes Ganzzahl-Eingabefeld + "Setzen"-Button. <paramref name="onSet"/> bekommt den
        /// geparsten Wert (geklemmt auf <paramref name="min"/>/<paramref name="max"/>, falls gesetzt).</summary>
        public static Row NewIntInputRow(string label, Action<int> onSet, int? min = null, int? max = null,
                                         string hint = null, string setLabel = "Setzen")
            => NewInputRow(label, hint, setLabel, raw =>
            {
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return false;
                if (min.HasValue && n < min.Value) n = min.Value;
                if (max.HasValue && n > max.Value) n = max.Value;
                onSet(n);
                return true;
            });

        /// <summary>Beschriftetes Dezimal-Eingabefeld + "Setzen"-Button (z. B. fuer Geschwindigkeit).</summary>
        public static Row NewFloatInputRow(string label, Action<float> onSet, float? min = null, float? max = null,
                                           string hint = null, string setLabel = "Setzen")
            => NewInputRow(label, hint, setLabel, raw =>
            {
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return false;
                if (min.HasValue && f < min.Value) f = min.Value;
                if (max.HasValue && f > max.Value) f = max.Value;
                onSet(f);
                return true;
            });

        // Das rohe Unity-TextField ist ungestylt und auf dem dunklen Panel kaum erkennbar. Wir geben ihm
        // einen sichtbaren Rahmen + dunklen Hintergrund + hellen Text (angelehnt an den CoI-Eingabefeld-Look).
        private static void StyleInputField(UnityEngine.UIElements.TextField field)
        {
            var bg = new UnityEngine.Color(0.09f, 0.11f, 0.15f, 1f);
            var border = new UnityEngine.Color(0.48f, 0.53f, 0.60f, 1f);

            var s = field.style;
            s.width = 130f;
            s.height = 26f;
            s.backgroundColor = bg;
            s.color = UnityEngine.Color.white;
            s.paddingLeft = 6f; s.paddingRight = 6f;
            s.borderTopWidth = 1f; s.borderBottomWidth = 1f; s.borderLeftWidth = 1f; s.borderRightWidth = 1f;
            s.borderTopColor = border; s.borderBottomColor = border; s.borderLeftColor = border; s.borderRightColor = border;
            s.borderTopLeftRadius = 3f; s.borderTopRightRadius = 3f; s.borderBottomLeftRadius = 3f; s.borderBottomRightRadius = 3f;

            // Inneres Eingabe-Element (haelt den eigentlichen Text) ebenfalls einfaerben, sonst ueberdeckt dessen
            // Default-Hintergrund unsere Farbe.
            var input = UnityEngine.UIElements.UQueryExtensions.Q(field, "unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = bg;
                input.style.color = UnityEngine.Color.white;
            }
        }

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
