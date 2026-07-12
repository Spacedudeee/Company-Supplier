using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Wetter" (ui-spec): fixiert das Wetter dauerhaft oder gibt den natuerlichen Zyklus frei.
    /// Eine Button-Gruppe (vier Wetterarten als Aktions-Buttons) + ein Reset-Button:
    /// - Sonnig     -> Weather.SetSunny()
    /// - Bewölkt    -> Weather.SetCloudy()
    /// - Regen      -> Weather.SetRainy()
    /// - Starkregen -> Weather.SetHeavyRain()
    /// - Zurücksetzen -> Weather.ResetWeather()
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class WetterTab : ICheatTab
    {
        private readonly UiComponent _content;

        public WetterTab()
        {
            _content = BuildContent();
        }

        public string Name => L.Tab_Wetter;

        // Temperature-Icon (Thermometer) — das Spiel hat kein Sonne/Wolke-Asset; das ist das naechstliegende
        // saubere Wetter/Klima-Glyph und passt stilistisch zu den uebrigen Reiter-Icons. (Fog.svg war eine Welle.)
        public string IconPath => "Assets/Unity/UserInterface/General/Temperature.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Wet_TitleFix),
                BuildWeatherButtons(),
                CheatWidgets.SectionTitle(L.Wet_TitleCycle),
                BuildResetButton()
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // Vier Wetter-Buttons in einer Zeile (neutral/General — keiner ist "gut" oder "gefaehrlich").
        private UiComponent BuildWeatherButtons()
        {
            var sunny = new ButtonText(
                Button.General, new LocStrFormatted(L.Wet_Sunny),
                () =>
                {
                    CheatService.Instance?.Weather?.SetSunny();
                    CheatMenuStatus.Show(L.Wet_StatusSunny);
                });
            var cloudy = new ButtonText(
                Button.General, new LocStrFormatted(L.Wet_Cloudy),
                () =>
                {
                    CheatService.Instance?.Weather?.SetCloudy();
                    CheatMenuStatus.Show(L.Wet_StatusCloudy);
                });
            var rainy = new ButtonText(
                Button.General, new LocStrFormatted(L.Wet_Rainy),
                () =>
                {
                    CheatService.Instance?.Weather?.SetRainy();
                    CheatMenuStatus.Show(L.Wet_StatusRainy);
                });
            var heavyRain = new ButtonText(
                Button.General, new LocStrFormatted(L.Wet_HeavyRain),
                () =>
                {
                    CheatService.Instance?.Weather?.SetHeavyRain();
                    CheatMenuStatus.Show(L.Wet_StatusHeavyRain);
                });

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(sunny, cloudy, rainy, heavyRain);
            return row;
        }

        // Hebt die Fixierung auf und gibt den natuerlichen Wetterzyklus wieder frei.
        private UiComponent BuildResetButton()
        {
            return CheatWidgets.DangerButton(
                L.Common_Reset,
                () =>
                {
                    CheatService.Instance?.Weather?.ResetWeather();
                    CheatMenuStatus.Show(L.Wet_StatusReset);
                },
                L.Wet_ResetTip);
        }
    }
}
