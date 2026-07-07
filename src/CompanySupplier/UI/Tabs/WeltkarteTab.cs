using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Weltkarte": deckt die Karte auf und schaltet die strategische Welt-Ebene scharf
    /// (unbegrenzte Welt-Minen, Welt-Minen ohne Unity, Effizienz-Boost, Handels-Boost).
    ///
    /// Bindet ueber <c>CheatService.Instance</c>. Registrierung per <c>[GlobalDependency(AsEverything)]</c>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class WeltkarteTab : ICheatTab
    {
        private readonly UiComponent _content;

        private Toggle _unlimitedMines, _minesNoUnity, _minesEffMax, _tradeBoost;
        private bool _suppress;

        public WeltkarteTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(WeltkarteTab), SyncFromState);
        }

        /// <summary>Zieht alle Toggle-Zustaende dieses Tabs aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                _unlimitedMines?.Value(Svc?.WorldMap?.UnlimitedMines ?? false);
                _minesNoUnity?.Value(Svc?.WorldMap?.MinesNoUnity ?? false);
                _minesEffMax?.Value(Svc?.WorldMap?.MinesEfficiencyMax ?? false);
                _tradeBoost?.Value(Svc?.WorldMap?.TradeBoosted ?? false);
            }
            finally { _suppress = false; }
        }

        public string Name => "Weltkarte";

        // WorldMap-Toolbar-Icon — die buchstaebliche Weltkarte (rendert auch bei Wetter als Fallback).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/WorldMap.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            // Toggles erst in ihre Felder bauen (Collection-Initializer erlauben keine Zuweisung).
            _unlimitedMines = CheatWidgets.NewToggleRow(
                "Unbegrenzte Welt-Minen",
                Svc?.WorldMap?.UnlimitedMines ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetUnlimitedMines(v); },
                "Welt-Minen-Vorkommen erschöpfen nicht mehr.");
            _minesNoUnity = CheatWidgets.NewToggleRow(
                "Welt-Minen ohne Unity betreiben",
                Svc?.WorldMap?.MinesNoUnity ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetMinesNoUnity(v); },
                "Welt-Minen laufen, ohne Unity zu verbrauchen.");
            _minesEffMax = CheatWidgets.NewToggleRow(
                "Welt-Minen-Effizienz max",
                Svc?.WorldMap?.MinesEfficiencyMax ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetMinesEfficiencyMax(v); },
                "Erhöht die Förderleistung der Welt-Minen deutlich.");
            _tradeBoost = CheatWidgets.NewToggleRow(
                "Handel boosten",
                Svc?.WorldMap?.TradeBoosted ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetTradeBoost(v); },
                "Mehr Handelsvolumen und Kontrakt-Gewinn; Kontrakte kosten keine Unity mehr.");

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Karte"),
                CheatWidgets.PrimaryButton(
                    "Ganze Karte aufdecken",
                    () =>
                    {
                        Svc?.WorldMap?.RevealMap();
                        CheatMenuStatus.Show("Weltkarte komplett aufgedeckt");
                    },
                    "Deckt die gesamte Weltkarte auf und löst alle Welt-Entitäten auf."),

                CheatWidgets.SectionTitle("Welt-Minen"),
                _unlimitedMines,
                _minesNoUnity,
                _minesEffMax,

                CheatWidgets.SectionTitle("Handel"),
                _tradeBoost
            };

            column.SetChildren(children.ToArray());
            return column;
        }
    }
}
