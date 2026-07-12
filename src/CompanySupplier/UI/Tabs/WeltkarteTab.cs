using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

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

        private Toggle _unlimitedMines, _minesNoUnity, _minesEffMax, _tradeBoost, _rocketCapacity;
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
                _rocketCapacity?.Value(Svc?.Gameplay?.RocketCapacityBoost ?? false);
            }
            finally { _suppress = false; }
        }

        public string Name => L.Tab_Weltkarte;

        // WorldMap-Toolbar-Icon — die buchstaebliche Weltkarte (rendert auch bei Wetter als Fallback).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/WorldMap.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            // Toggles erst in ihre Felder bauen (Collection-Initializer erlauben keine Zuweisung).
            _unlimitedMines = CheatWidgets.NewToggleRow(
                L.Wlt_UnlimitedMines,
                Svc?.WorldMap?.UnlimitedMines ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetUnlimitedMines(v); },
                L.Wlt_UnlimitedMinesTip);
            _minesNoUnity = CheatWidgets.NewToggleRow(
                L.Wlt_MinesNoUnity,
                Svc?.WorldMap?.MinesNoUnity ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetMinesNoUnity(v); },
                L.Wlt_MinesNoUnityTip);
            _minesEffMax = CheatWidgets.NewToggleRow(
                L.Wlt_MinesEffMax,
                Svc?.WorldMap?.MinesEfficiencyMax ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetMinesEfficiencyMax(v); },
                L.Wlt_MinesEffMaxTip);
            _tradeBoost = CheatWidgets.NewToggleRow(
                L.Wlt_TradeBoost,
                Svc?.WorldMap?.TradeBoosted ?? false,
                v => { if (!_suppress) Svc?.WorldMap?.SetTradeBoost(v); },
                L.Wlt_TradeBoostTip);
            _rocketCapacity = CheatWidgets.NewToggleRow(
                L.Wlt_RocketCapacity,
                Svc?.Gameplay?.RocketCapacityBoost ?? false,
                v => { if (!_suppress) Svc?.Gameplay?.SetRocketCapacityBoost(v); },
                L.Wlt_RocketCapacityTip);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Wlt_TitleMap),
                CheatWidgets.PrimaryButton(
                    L.Wlt_RevealMap,
                    () =>
                    {
                        Svc?.WorldMap?.RevealMap();
                        CheatMenuStatus.Show(L.Wlt_StatusRevealed);
                    },
                    L.Wlt_RevealMapTip),

                CheatWidgets.SectionTitle(L.Wlt_TitleMines),
                _unlimitedMines,
                _minesNoUnity,
                _minesEffMax,

                CheatWidgets.SectionTitle(L.Wlt_TitleTrade),
                _tradeBoost,
                _rocketCapacity
            };

            column.SetChildren(children.ToArray());
            return column;
        }
    }
}
