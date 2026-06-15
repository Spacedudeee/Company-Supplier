using System;
using System.Reflection;
using Mafi;
using Mafi.Base;
using Mafi.Core.Environment;
using Mafi.Core.Prototypes;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Wetter-Cheats: fixiert das Wetter dauerhaft auf Sunny/Cloudy/Rainy/HeavyRain oder gibt den
    /// natuerlichen Wetterzyklus wieder frei.
    ///
    /// In 0.8.5.0 stellt der <see cref="WeatherManager"/> echte public Cheat-APIs bereit
    /// (<c>Cheat_TrySetWeatherFixed(Proto.ID)</c> + <c>ClearOverride()</c>) — der frühere
    /// Reflection-Hack auf <c>m_weatherOverrideDuration</c> entfaellt damit. Die Property
    /// <c>WeatherManager.HasWeatherOverride</c> zeigt an, ob aktuell fixiert ist.
    ///
    /// Robustheit: <see cref="IWeatherManager"/> wird via DI aufgeloest und auf den konkreten
    /// <see cref="WeatherManager"/> gecastet (dort liegen die Cheat-Methoden). Jeder Cheat ist in
    /// try/catch gekapselt und schreibt bei Fehlern nur ins Log.
    /// </summary>
    public sealed class WeatherCheats
    {
        private readonly DependencyResolver _resolver;
        private IWeatherManager _weatherManager;

        public WeatherCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IWeatherManager>(out _weatherManager);
        }

        // -- Saubere Methoden je Wetterart (dauerhaft fixiert) -------------------------------------

        /// <summary>Fixiert das Wetter dauerhaft auf Sonnenschein.</summary>
        public void SetSunny() => SetWeatherFixed(Ids.Weather.Sunny);

        /// <summary>Fixiert das Wetter dauerhaft auf bewoelkt.</summary>
        public void SetCloudy() => SetWeatherFixed(Ids.Weather.Cloudy);

        /// <summary>Fixiert das Wetter dauerhaft auf Regen.</summary>
        public void SetRainy() => SetWeatherFixed(Ids.Weather.Rainy);

        /// <summary>Fixiert das Wetter dauerhaft auf starken Regen.</summary>
        public void SetHeavyRain() => SetWeatherFixed(Ids.Weather.HeavyRain);

        /// <summary>
        /// Fixiert das Wetter dauerhaft anhand eines String-Bezeichners
        /// ("Sunny", "Cloudy", "Rainy", "HeavyRain"; Gross-/Kleinschreibung egal).
        /// Unbekannte Werte werden ignoriert (Log.Warning).
        /// </summary>
        public void SetWeather(string weatherIdOrEnum)
        {
            if (string.IsNullOrWhiteSpace(weatherIdOrEnum)) return;
            switch (weatherIdOrEnum.Trim().ToLowerInvariant())
            {
                case "sunny": SetSunny(); break;
                case "cloudy": SetCloudy(); break;
                case "rainy": case "rain": SetRainy(); break;
                case "heavyrain": case "heavy_rain": case "heavy rain": SetHeavyRain(); break;
                default:
                    Log.Warning($"[CompanySupplier] SetWeather: unbekannter Wert '{weatherIdOrEnum}'.");
                    break;
            }
        }

        /// <summary>Hebt die Fixierung auf und gibt den natuerlichen Wetterzyklus wieder frei.</summary>
        public void ResetWeather()
        {
            if (_weatherManager == null) return;
            try
            {
                // 0.8.5.0: public WeatherManager.ClearOverride() statt Override-Duration=0-Reflection.
                if (_weatherManager is WeatherManager wm)
                {
                    wm.ClearOverride();
                }
                else
                {
                    CallNonPublic(_weatherManager, "ClearOverride");
                }
                Log.Info("[CompanySupplier] Wetter-Fixierung aufgehoben (natuerlicher Zyklus).");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] ResetWeather: {ex.Message}");
            }
        }

        // -- Kern --------------------------------------------------------------------------------

        private void SetWeatherFixed(Proto.ID weatherId)
        {
            if (_weatherManager == null) return;
            try
            {
                // 0.8.5.0: public WeatherManager.Cheat_TrySetWeatherFixed(Proto.ID) — setzt das Wetter
                // und fixiert es permanent (kein Zuruecksetzen durch den Sim-Loop).
                bool ok;
                if (_weatherManager is WeatherManager wm)
                {
                    ok = wm.Cheat_TrySetWeatherFixed(weatherId);
                }
                else
                {
                    var result = CallNonPublic(_weatherManager, "Cheat_TrySetWeatherFixed", weatherId);
                    ok = result is bool b && b;
                }

                if (ok) Log.Info($"[CompanySupplier] Wetter dauerhaft fixiert: {weatherId}.");
                else Log.Warning($"[CompanySupplier] Wetter '{weatherId}' konnte nicht gesetzt werden.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] SetWeatherFixed({weatherId}): {ex.Message}");
            }
        }

        /// <summary>
        /// Ruft eine internal/private Instanzmethode per Reflection auf (Fallback, falls der
        /// aufgeloeste Manager nicht auf den konkreten WeatherManager castbar ist).
        /// </summary>
        private static object CallNonPublic(object target, string method, params object[] args)
        {
            var argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i]?.GetType() ?? typeof(object);

            var mi = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null, types: argTypes, modifiers: null)
                ?? target.GetType().GetMethod(method,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            return mi?.Invoke(target, args.Length == 0 ? null : args);
        }
    }
}
