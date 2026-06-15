using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;   // Padding/TinyFontSize/.Value-Extensions fuer die Statuszeile
using Mafi.Unity.UiToolkit.Library;

namespace CompanySupplier.UI
{
    /// <summary>
    /// Baut das eigentliche Fenster (Titel "Company Supplier", verschiebbar, Tab-Leiste) aus allen per DI
    /// gefundenen <see cref="ICheatTab"/>-Implementierungen. In 0.8.5.0 ersetzt die UiToolkit-Klasse
    /// <see cref="Window"/> die alte <c>WindowView</c>; daher erzeugt diese Klasse pro Aufruf eine frische
    /// <see cref="Window"/> (kein dauerhaftes View-Objekt mehr) — der Controller oeffnet/schliesst sie.
    ///
    /// Registrierung: <c>[GlobalDependency(AsSelf)]</c> -> der Container injiziert diese Klasse in den
    /// <see cref="CheatMenuController"/>; sie selbst zieht ueber <c>AllImplementationsOf&lt;ICheatTab&gt;</c>
    /// alle Tabs ein.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsSelf)]
    public sealed class CheatMenuWindowView
    {
        // Breiter + höher als der erste Wurf: elf Tabs brauchen Platz (sonst staucht die Tab-Schrift),
        // und die Controls (Slider, Dropdown) sollen sich entfalten können.
        private const int WindowWidthPx = 1100;
        private const int WindowHeightPx = 640;

        /// <summary>Feste, vom Design vorgegebene Reiter-Reihenfolge (Headliner zuerst, Werkzeuge danach).
        /// Tabs, deren Name hier nicht vorkommt, werden hinten alphabetisch angehaengt. Muss mit der
        /// Reihenfolge in der README-Funktionstabelle uebereinstimmen.</summary>
        private static readonly string[] TabOrder =
        {
            "Ressourcen", "Sandbox", "Umwelt", "Weltkarte",
            "Allgemein", "Erzeugung", "Werft & Flotte", "Fahrzeuge", "Gelände", "Wetter", "Profil"
        };

        private readonly IReadOnlyList<ICheatTab> _tabs;

        public CheatMenuWindowView(AllImplementationsOf<ICheatTab> cheatTabs)
        {
            // MaFi's ImmutableArray<T> ist ein eigener Typ (nicht System.Collections.Immutable) — .AsEnumerable()
            // liefert ein System.IEnumerable<T> fuer die LINQ-Sortierung.
            _tabs = OrderTabs(cheatTabs.Implementations.AsEnumerable());
            Log.Info($"[{CompanySupplier.ModName}] CheatMenuWindowView: {_tabs.Count} Tabs gefunden.");
        }

        /// <summary>Erzeugt eine neue, vollstaendig aufgebaute Fenster-Instanz.</summary>
        public Window BuildWindow()
        {
            var window = new Window(new LocStrFormatted("Company Supplier"), addFullscreenButton: false)
                // WindowWidth = FESTE Breite (WindowSize war nur max -> Fenster schrumpfte auf den Inhalt,
                // dadurch enge Tabs + winzige Slider). WindowMaxHeight begrenzt die Höhe (Inhalt scrollt).
                .WindowWidth((Px)WindowWidthPx)
                .WindowMaxHeight(Percent.FromPercentVal(85))
                .MakeMovable();
            window.CloseOnClickOutside(); // schliesst bei Klick ausserhalb (komfortabel fuer ein Werkzeug-Fenster)

            var tabContainer = new TabContainer().ReducedPaddingBody();
            foreach (var tab in _tabs)
            {
                Log.Info($"[{CompanySupplier.ModName}]   + Tab '{tab.Name}'.");
                // AddTab nimmt den Icon-Pfad als String (kein IconStyle mehr in 0.8.5.0).
                tabContainer.AddTab(
                    new LocStrFormatted(tab.Name),
                    tab.Content,
                    tab.IconPath ?? string.Empty,
                    tooltip: null,
                    switchTo: false,
                    scroll: true,
                    index: null);
            }

            // Persistente Statuszeile: ZWEITES Kind der Body-Column, also UNTER dem TabContainer und
            // AUSSERHALB des per-Tab-Scrollbereichs (scroll:true sitzt im TabContainer) -> bleibt immer
            // sichtbar. Kein RenderUpdate-Hook noetig: CheatMenuStatus.Show(...) setzt das Label sofort,
            // event-getrieben beim Button-Klick.
            var statusLabel = new Label(LocStrFormatted.Empty)
                .TinyFontSize()
                .PaddingTopBottom((Px)6)
                .PaddingLeft((Px)10);
            CheatMenuStatus.Bind(statusLabel); // Referenz beim Bau registrieren (Window wird je F8-Toggle neu gebaut)

            // EIN AddBodySingle-Aufruf mit beiden Kindern (params UiComponent[]) — ein zweiter Aufruf
            // wuerde den Body neu setzen und den TabContainer verdraengen.
            window.AddBodySingle(tabContainer, statusLabel);
            return window;
        }

        private static IReadOnlyList<ICheatTab> OrderTabs(IEnumerable<ICheatTab> tabs)
        {
            var byName = tabs.ToList();
            return byName
                .OrderBy(t =>
                {
                    int idx = System.Array.IndexOf(TabOrder, t.Name);
                    return idx < 0 ? int.MaxValue : idx;
                })
                .ThenBy(t => t.Name)
                .ToList();
        }
    }
}
