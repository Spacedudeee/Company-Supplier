using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;   // UiComponentWithTextExtensions (.Value)
using Mafi.Unity.UiToolkit.Library;     // Label

namespace CompanySupplier.UI
{
    /// <summary>
    /// Zentrale Statuszeile des Cheat-Fensters. Tabs rufen <see cref="Show"/> auf;
    /// die Meldung erscheint unten im Fenster (ausserhalb des scrollenden Tab-Bodys).
    /// Die Label-Referenz wird bei jedem Fensterbau (<c>CheatMenuWindowView.BuildWindow</c>)
    /// neu gesetzt — bei geschlossenem Fenster ist <c>_label</c> null und <see cref="Show"/>
    /// ist ein No-Op (kein Crash). Bewusst statisch: die Tabs sind [GlobalDependency]-Singletons,
    /// die ihren Content einmalig im Ctor bauen und nur statisch auf diese Zeile verweisen — sie
    /// brauchen keine eigene Label-Referenz.
    /// </summary>
    internal static class CheatMenuStatus
    {
        private static Label _label;

        /// <summary>Wird von <c>CheatMenuWindowView.BuildWindow()</c> mit dem frisch gebauten Label aufgerufen.</summary>
        internal static void Bind(Label label) => _label = label;

        /// <summary>Setzt die Statusmeldung (z. B. "250x Eisen hinzugefuegt"). Sicher bei geschlossenem Fenster.</summary>
        public static void Show(string message)
        {
            _label?.Value(new LocStrFormatted(message ?? string.Empty));
        }

        /// <summary>Leert die Zeile.</summary>
        public static void Clear() => _label?.Value(LocStrFormatted.Empty);
    }
}
