using System.Globalization;
using Mafi.Localization;

namespace CompanySupplier.Localization
{
    /// <summary>Vom Mod unterstützte Menü-Sprachen. Reihenfolge/Werte sind stabil (nicht umsortieren).</summary>
    internal enum ModLang { En = 0, De = 1, Fr = 2, Es = 3 }

    /// <summary>
    /// Zentrale Sprachauswahl des Mods. Die Menü-Sprache folgt der <b>Spielsprache</b>:
    /// <c>LocalizationManager.CurrentCultureInfo</c> liefert die aktuell im Spiel gewählte Kultur
    /// (z. B. "de-DE"); daraus wird der Zwei-Buchstaben-Code (de/fr/es) auf eine <see cref="ModLang"/>
    /// abgebildet. Alles andere fällt auf Englisch zurück (die Referenz-/Basissprache des Mods).
    ///
    /// Die Erkennung wird einmalig gecacht: die Tab-Inhalte werden nur EINMAL beim DI-Aufbau gebaut,
    /// die Sprache ändert sich innerhalb einer Sitzung praktisch nicht. Ein Sprachwechsel im Spiel greift
    /// nach dem nächsten Spielstand-Laden (neue Tab-Instanzen). Robuste Fehlerbehandlung: schlägt der
    /// Zugriff auf die Spiel-Localization fehl (sehr früher Aufruf), greift die UI-Kultur, dann Englisch.
    /// </summary>
    internal static class Loc
    {
        private static ModLang? _cached;

        /// <summary>Aktuelle Menü-Sprache (gecacht ab dem ersten Zugriff).</summary>
        public static ModLang Lang => _cached ?? (ModLang)(_cached = Detect());

        /// <summary>Nur für Tests/Sonderfälle: erzwingt eine Neuerkennung beim nächsten Zugriff.</summary>
        internal static void Reset() => _cached = null;

        private static ModLang Detect()
        {
            try
            {
                CultureInfo ci = SafeCulture();
                switch (ci?.TwoLetterISOLanguageName)
                {
                    case "de": return ModLang.De;
                    case "fr": return ModLang.Fr;
                    case "es": return ModLang.Es;
                    default:   return ModLang.En;
                }
            }
            catch { return ModLang.En; }
        }

        // Spiel-Kultur bevorzugen; bei sehr frühem Aufruf (Localization noch nicht scharf) auf die
        // UI-Kultur ausweichen, sonst null (-> Englisch-Fallback).
        private static CultureInfo SafeCulture()
        {
            try
            {
                CultureInfo game = LocalizationManager.CurrentCultureInfo;
                if (game != null) return game;
            }
            catch { /* Localization noch nicht initialisiert */ }

            try { return CultureInfo.CurrentUICulture; }
            catch { return null; }
        }

        /// <summary>Wählt den Text der aktiven Menü-Sprache (Englisch = Fallback).</summary>
        public static string T(string en, string de, string fr, string es)
        {
            switch (Lang)
            {
                case ModLang.De: return de;
                case ModLang.Fr: return fr;
                case ModLang.Es: return es;
                default:         return en;
            }
        }
    }
}
