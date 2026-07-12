using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;   // SetVisible/SetChildren/Width/FlexGrow/OnClick/Padding-Extensions
using Mafi.Unity.UiToolkit.Library;

namespace CompanySupplier.UI
{
    /// <summary>
    /// Baut das eigentliche Fenster (Titel "Company Supplier", verschiebbar) aus allen per DI gefundenen
    /// <see cref="ICheatTab"/>-Implementierungen. Pro Aufruf entsteht eine frische <see cref="Window"/>
    /// (der Controller oeffnet/schliesst sie je F8-Toggle).
    ///
    /// Layout (ab v2.0): Bei so vielen Reitern wird die klassische horizontale Tab-Leiste zu eng — daher eine
    /// VERTIKALE Reiter-Leiste LINKS (Button-Spalte) plus einen scrollbaren Inhaltsbereich rechts. Das
    /// Spiel-<c>TabContainer</c> kann nur horizontal (Tab-Leiste ist eine <c>Row</c>), deshalb bauen wir
    /// die Seiten-Navigation selbst — nach demselben Muster wie der TabContainer: ALLE Tab-Inhalte werden
    /// gehalten, aber nur der aktive ist via <c>SetVisible</c> sichtbar (kein Re-Parenting beim Umschalten).
    ///
    /// Registrierung: <c>[GlobalDependency(AsSelf)]</c> -> injiziert in den <see cref="CheatMenuController"/>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsSelf)]
    public sealed class CheatMenuWindowView
    {
        private const int WindowWidthPx = 1100;
        private const int SidebarWidthPx = 220;

        // FESTE Body-Hoehe -> das Fenster ist in JEDEM Reiter gleich gross (sonst waechst es mit dem Inhalt
        // und „springt" je Tab). Reiter mit mehr Inhalt (z. B. Sandbox) scrollen im rechten Bereich. Tunable.
        private const int BodyHeightPx = 500;

        /// <summary>Feste, vom Design vorgegebene Reiter-Reihenfolge (Headliner zuerst, Werkzeuge danach).
        /// Tabs, deren Typ hier nicht vorkommt, werden hinten alphabetisch (nach Anzeigename) angehaengt.
        /// Muss mit der Reihenfolge in der README-Funktionstabelle uebereinstimmen.
        /// WICHTIG: Sortierung ueber den TYP-Namen (sprachunabhaengig), NICHT ueber den lokalisierten
        /// <c>Name</c> — sonst zerfiele die Reihenfolge, sobald das Menue in einer anderen Sprache laeuft.</summary>
        private static readonly string[] TabOrder =
        {
            nameof(Tabs.RessourcenTab), nameof(Tabs.AllgemeinTab), nameof(Tabs.UmweltTab), nameof(Tabs.WeltkarteTab),
            nameof(Tabs.ErzeugungTab), nameof(Tabs.ProduktionTab), nameof(Tabs.WerftFlotteTab), nameof(Tabs.FahrzeugeTab),
            nameof(Tabs.GelaendeTab), nameof(Tabs.WetterTab), nameof(Tabs.ProfilTab)
        };

        private readonly IReadOnlyList<ICheatTab> _tabs;

        // Zuletzt aktiver Reiter — Instanz-State (CheatMenuWindowView ist Singleton), bleibt also ueber
        // F8-Oeffnungen hinweg erhalten.
        private int _activeIndex;

        // Wird je Fenster-Aufbau neu gesetzt; RefreshSidebar haengt die aktuellen Buttons hier ein.
        private Column _sidebar;

        public CheatMenuWindowView(AllImplementationsOf<ICheatTab> cheatTabs)
        {
            // MaFi's ImmutableArray<T> ist ein eigener Typ — .AsEnumerable() liefert IEnumerable<T> fuer LINQ.
            _tabs = OrderTabs(cheatTabs.Implementations.AsEnumerable());

            // Zuletzt geoeffneten Reiter aus der Config wiederherstellen (ueber Spielstarts hinweg). Config kann
            // bei sehr fruehem Bau noch fehlen -> Fallback 0; danach gegen den gueltigen Bereich klemmen.
            _activeIndex = CheatService.Instance?.Config?.LastTabIndex ?? 0;
            if (_activeIndex < 0 || _activeIndex >= _tabs.Count) _activeIndex = 0;
            Log.Info($"[{CompanySupplier.ModName}] CheatMenuWindowView: {_tabs.Count} Tabs gefunden.");
        }

        /// <summary>Erzeugt eine neue, vollstaendig aufgebaute Fenster-Instanz.</summary>
        public Window BuildWindow()
        {
            var window = new Window(new LocStrFormatted("Company Supplier"), addFullscreenButton: false)
                .WindowWidth((Px)WindowWidthPx)
                .WindowMaxHeight(Percent.FromPercentVal(85))
                .MakeMovable();
            window.CloseOnClickOutside();

            // Linke, vertikale Reiter-Leiste: feste Breite, schrumpft nicht; Buttons fuellen die Breite.
            _sidebar = new Column((Px)4)
                .AlignItemsStretch()
                .Padding((Px)8)
                .Width((Px)SidebarWidthPx)
                .FlexShrink(0f);

            // Inhaltsbereich rechts: nimmt die Restbreite (FlexGrow) UND streckt seine Kinder auf volle Breite
            // (AlignItemsStretch) -> Slider/Dropdowns sind in JEDEM Reiter gleich breit. Scrollbar, falls der
            // Inhalt hoeher als BodyHeightPx ist (haelt ALLE Tab-Inhalte, nur der aktive ist sichtbar).
            var content = new ScrollColumn().FlexGrow(1f).AlignItemsStretch();
            content.SetChildren(_tabs.Select(t => t.Content));

            // Reiter-Leiste + Inhalt nebeneinander, mit FESTER Hoehe -> konstante Fenstergroesse ueber alle Tabs.
            // AlignItemsStretch streckt Sidebar UND Inhalt auf die volle Hoehe, damit der Inhalt immer OBEN
            // beginnt (statt vertikal zentriert zu werden, wenn ein Reiter wenig Inhalt hat).
            var bodyRow = new Row((Px)0).AlignItemsStretch().Height((Px)BodyHeightPx);
            bodyRow.SetChildren(_sidebar, content);

            RefreshSidebarAndVisibility(); // Buttons hervorheben + Sichtbarkeit gemaess _activeIndex

            // Persistente Statuszeile unter dem Body (ausserhalb des Scrollbereichs -> immer sichtbar).
            // Mit dezenter Trennlinie darueber als eigene "Statusleisten"-Zone abgesetzt.
            var statusLabel = new Label(LocStrFormatted.Empty)
                .TinyFontSize()
                .PaddingTopBottom((Px)8)
                .PaddingLeft((Px)10);
            CheatMenuStatus.Bind(statusLabel);

            var statusBar = new Column((Px)0).AlignItemsStretch();
            statusBar.SetChildren(CheatWidgets.Separator(0), statusLabel);

            // Toggle-/Anzeige-Zustaende aus dem Backend nachziehen: die Tab-Inhalte wurden nur einmal
            // (im DI-Ctor) gebaut — ohne diesen Sync zeigt jedes neu geoeffnete Fenster den Zustand
            // von damals (z. B. nach externem Werkzeug-Deactivate oder Zustandsaenderungen per Hotkey).
            CheatUiSync.SyncAll();

            window.AddBodySingle(bodyRow, statusBar);
            return window;
        }

        /// <summary>Baut die Sidebar-Buttons neu (aktiver Reiter = Primary/hervorgehoben, sonst General) und
        /// schaltet die Sichtbarkeit der Tab-Inhalte (nur der aktive ist sichtbar).</summary>
        private void RefreshSidebarAndVisibility()
        {
            if (_sidebar == null) return;

            var buttons = new List<UiComponent>(_tabs.Count + 1);
            for (int i = 0; i < _tabs.Count; i++)
            {
                // Dezente Trennlinie zwischen den Cheat-Reitern und dem Verwaltungs-Reiter "Profil".
                if (_tabs[i] is Tabs.ProfilTab && i > 0)
                    buttons.Add(CheatWidgets.Separator());
                buttons.Add(BuildNavButton(i));
            }
            _sidebar.SetChildren(buttons.ToArray());

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Content.SetVisible(i == _activeIndex);
        }

        /// <summary>Baut einen Sidebar-Reiter-Button. Liefert der Tab einen <c>IconPath</c>, kommt ein
        /// <see cref="ButtonIconText"/> (Icon + Text) zum Einsatz, sonst ein reiner <see cref="ButtonText"/>
        /// (leerer Pfad -> kein Broken-Icon). Aktiver Reiter = Primary (hervorgehoben), sonst General.
        /// <see cref="ButtonIconText"/> kennt keinen onClick-Ctor, daher wird der Klick per
        /// <c>OnClick</c>-Extension nachgeruestet. <paramref name="index"/> ist je Aufruf eigen -> closure-sicher.</summary>
        private UiComponent BuildNavButton(int index)
        {
            var variant = (index == _activeIndex) ? Button.Primary : Button.General;
            var label = new LocStrFormatted(_tabs[index].Name);
            string iconPath = _tabs[index].IconPath;

            if (!string.IsNullOrEmpty(iconPath))
                return new ButtonIconText(variant, iconPath, label).OnClick(() => SwitchTo(index));

            return new ButtonText(variant, label, () => SwitchTo(index));
        }

        /// <summary>Wechselt zum Reiter <paramref name="index"/> (hebt den Button hervor, blendet den Inhalt ein).</summary>
        private void SwitchTo(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            _activeIndex = index;
            RefreshSidebarAndVisibility();
            PersistActiveTab();
        }

        /// <summary>Merkt sich den aktiven Reiter in der Config (best-effort), damit er ueber Spielstarts
        /// erhalten bleibt. Schreibt nur bei tatsaechlicher Aenderung -> kein unnoetiger Datei-I/O.</summary>
        private void PersistActiveTab()
        {
            var svc = CheatService.Instance;
            if (svc?.Config == null || svc.Config.LastTabIndex == _activeIndex) return;
            svc.Config.LastTabIndex = _activeIndex;
            svc.SaveConfig();
        }

        private static IReadOnlyList<ICheatTab> OrderTabs(IEnumerable<ICheatTab> tabs)
        {
            // Ueber den TYP-Namen sortieren (stabil, sprachunabhaengig) — der Anzeigename ist lokalisiert.
            return tabs
                .OrderBy(t =>
                {
                    int idx = System.Array.IndexOf(TabOrder, t.GetType().Name);
                    return idx < 0 ? int.MaxValue : idx;
                })
                .ThenBy(t => t.GetType().Name)
                .ToList();
        }
    }
}
