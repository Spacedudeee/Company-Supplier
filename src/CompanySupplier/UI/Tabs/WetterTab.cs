using System.Collections.Generic;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

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

        public string Name => "Wetter";

        // Temperature-Icon (Thermometer) — das Spiel hat kein Sonne/Wolke-Asset; das ist das naechstliegende
        // saubere Wetter/Klima-Glyph und passt stilistisch zu den uebrigen Reiter-Icons. (Fog.svg war eine Welle.)
        public string IconPath => "Assets/Unity/UserInterface/General/Temperature.svg";

        public UiComponent Content => _content;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Wetter dauerhaft fixieren"),
                BuildWeatherButtons(),
                CheatWidgets.SectionTitle("Natürlicher Zyklus"),
                BuildResetButton()
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // Vier Wetter-Buttons in einer Zeile (neutral/General — keiner ist "gut" oder "gefaehrlich").
        private UiComponent BuildWeatherButtons()
        {
            var sunny = new ButtonText(
                Button.General, new LocStrFormatted("Sonnig"),
                () =>
                {
                    CheatService.Instance?.Weather?.SetSunny();
                    CheatMenuStatus.Show("Wetter fixiert: Sonnig");
                });
            var cloudy = new ButtonText(
                Button.General, new LocStrFormatted("Bewölkt"),
                () =>
                {
                    CheatService.Instance?.Weather?.SetCloudy();
                    CheatMenuStatus.Show("Wetter fixiert: Bewölkt");
                });
            var rainy = new ButtonText(
                Button.General, new LocStrFormatted("Regen"),
                () =>
                {
                    CheatService.Instance?.Weather?.SetRainy();
                    CheatMenuStatus.Show("Wetter fixiert: Regen");
                });
            var heavyRain = new ButtonText(
                Button.General, new LocStrFormatted("Starkregen"),
                () =>
                {
                    CheatService.Instance?.Weather?.SetHeavyRain();
                    CheatMenuStatus.Show("Wetter fixiert: Starkregen");
                });

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(sunny, cloudy, rainy, heavyRain);
            return row;
        }

        // Hebt die Fixierung auf und gibt den natuerlichen Wetterzyklus wieder frei.
        private UiComponent BuildResetButton()
        {
            return CheatWidgets.DangerButton(
                "Zurücksetzen",
                () =>
                {
                    CheatService.Instance?.Weather?.ResetWeather();
                    CheatMenuStatus.Show("Wetter zurückgesetzt (natürlicher Zyklus)");
                },
                "Hebt die Wetter-Fixierung auf und gibt den natürlichen Wetterzyklus wieder frei.");
        }
    }
}
