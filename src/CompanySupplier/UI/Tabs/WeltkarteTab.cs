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

        public WeltkarteTab()
        {
            _content = BuildContent();
        }

        public string Name => "Weltkarte";

        public string IconPath => "";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

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
                CheatWidgets.NewToggleRow(
                    "Unbegrenzte Welt-Minen",
                    Svc?.WorldMap?.UnlimitedMines ?? false,
                    v => Svc?.WorldMap?.SetUnlimitedMines(v),
                    "Welt-Minen-Vorkommen erschöpfen nicht mehr."),
                CheatWidgets.NewToggleRow(
                    "Welt-Minen ohne Unity betreiben",
                    Svc?.WorldMap?.MinesNoUnity ?? false,
                    v => Svc?.WorldMap?.SetMinesNoUnity(v),
                    "Welt-Minen laufen, ohne Unity zu verbrauchen."),
                CheatWidgets.NewToggleRow(
                    "Welt-Minen-Effizienz max",
                    Svc?.WorldMap?.MinesEfficiencyMax ?? false,
                    v => Svc?.WorldMap?.SetMinesEfficiencyMax(v),
                    "Erhöht die Förderleistung der Welt-Minen deutlich."),

                CheatWidgets.SectionTitle("Handel"),
                CheatWidgets.NewToggleRow(
                    "Handel boosten",
                    Svc?.WorldMap?.TradeBoosted ?? false,
                    v => Svc?.WorldMap?.SetTradeBoost(v),
                    "Mehr Handelsvolumen und Kontrakt-Gewinn; Kontrakte kosten keine Unity mehr.")
            };

            column.SetChildren(children.ToArray());
            return column;
        }
    }
}
