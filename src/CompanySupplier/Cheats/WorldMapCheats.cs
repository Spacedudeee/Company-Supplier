using System;
using Mafi;
using Mafi.Core;                       // IdsCore.PropertyIds, PropertyId<T>
using Mafi.Core.PropertiesDb;          // IPropertiesDb, IProperty<T>, PropertyModifiers
using Mafi.Core.World;                 // IWorldMapManager

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Weltkarte": deckt die komplette Weltkarte auf und schaltet die strategische
    /// Welt-Ebene scharf — unbegrenzte Welt-Minen, Welt-Minen ohne Unity, Effizienz-Boost sowie ein
    /// Handels-Boost (mehr Volumen/Gewinn, gratis Kontrakt-Unity).
    ///
    /// 0.8.5.0 (per ApiInspector verifiziert):
    ///  - <c>IWorldMapManager.Cheat_RevealAndResolveAllEntities()</c> ist public -> Karte mit einem Aufruf
    ///    komplett aufdecken (keine Zell-Enumeration noetig).
    ///  - Welt-Minen/Handel sind globale Properties in <see cref="IdsCore.PropertyIds"/>; gesetzt ueber
    ///    eigene PropertyModifier (Owner <see cref="ModifierOwner"/>) — exakt das Muster aus
    ///    <see cref="PollutionCheats"/> / <see cref="FleetVehicleCheats"/>.
    ///
    /// Robustheit: Manager via TryResolve, jeder Cheat in try/catch + Log.Warning.
    /// </summary>
    public sealed class WorldMapCheats
    {
        private const string ModifierOwner = "CompanySupplier.WorldMap";

        // Boost-Hoehen (additive Deltas auf die 100%-Basis).
        private const int EfficiencyBoostPercent = 400;   // -> 500% = 5x Effizienz
        private const int TradeVolumeBoostPercent = 200;  // -> 300%
        private const int TradeProfitBoostPercent = 200;  // -> 300%

        private readonly DependencyResolver _resolver;
        private IWorldMapManager _worldMap;
        private IPropertiesDb _propertiesDb;

        // Fallback-Werte, falls die PropertiesDb nicht lesbar ist (zuletzt von uns gesetzter Zustand).
        private bool _unlimitedCached, _noUnityCached, _effMaxCached, _tradeCached;

        // Status live aus der Praesenz UNSERES Modifiers gelesen: PropertyModifier ueberleben im
        // Spielstand — reine Session-Mirror wuerden nach einem Save-Load luegen.
        public bool UnlimitedMines     => HasOurBoolModifier(IdsCore.PropertyIds.UnlimitedWorldMines, _unlimitedCached);
        public bool MinesNoUnity       => HasOurBoolModifier(IdsCore.PropertyIds.WorldMinesCanRunWithoutUnity, _noUnityCached);
        public bool MinesEfficiencyMax => HasOurPercentModifier(IdsCore.PropertyIds.WorldMinesEfficiency, _effMaxCached);
        // Der Handels-Boost besteht aus drei Modifiern; das Volumen ist der Leitindikator.
        public bool TradeBoosted       => HasOurPercentModifier(IdsCore.PropertyIds.TradeVolumeMultiplier, _tradeCached);

        private bool HasOurBoolModifier(PropertyId<bool> id, bool fallback)
        {
            try
            {
                IProperty<bool> prop = _propertiesDb?.GetProperty(id);
                if (prop != null) return prop.TryGetModifier(ModifierOwner, out PropertyModifier<bool> _);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Welt-Status lesen: {ex.Message}"); }
            return fallback;
        }

        private bool HasOurPercentModifier(PropertyId<Percent> id, bool fallback)
        {
            try
            {
                IProperty<Percent> prop = _propertiesDb?.GetProperty(id);
                if (prop != null) return prop.TryGetModifier(ModifierOwner, out PropertyModifier<Percent> _);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] Welt-Status lesen: {ex.Message}"); }
            return fallback;
        }

        public WorldMapCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<IWorldMapManager>(out _worldMap);
            _resolver.TryResolve<IPropertiesDb>(out _propertiesDb);
        }

        // -- Karte aufdecken ----------------------------------------------------------------------

        /// <summary>Deckt die gesamte Weltkarte auf und loest alle Welt-Entitaeten auf.</summary>
        public void RevealMap()
        {
            if (_worldMap == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] RevealMap: IWorldMapManager nicht aufgeloest.");
                return;
            }
            try
            {
                _worldMap.Cheat_RevealAndResolveAllEntities();
                Log.Info($"[{CompanySupplier.ModName}] Weltkarte komplett aufgedeckt.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] RevealMap: {ex.Message}");
            }
        }

        // -- Welt-Minen ---------------------------------------------------------------------------

        public void SetUnlimitedMines(bool enabled)
        {
            if (ApplyBool(IdsCore.PropertyIds.UnlimitedWorldMines, enabled, "Unbegrenzte Welt-Minen")) _unlimitedCached = enabled;
        }

        public void SetMinesNoUnity(bool enabled)
        {
            if (ApplyBool(IdsCore.PropertyIds.WorldMinesCanRunWithoutUnity, enabled, "Welt-Minen ohne Unity")) _noUnityCached = enabled;
        }

        public void SetMinesEfficiencyMax(bool enabled)
        {
            if (ApplyPercent(IdsCore.PropertyIds.WorldMinesEfficiency, enabled, EfficiencyBoostPercent, "Welt-Minen-Effizienz"))
                _effMaxCached = enabled;
        }

        // -- Handel -------------------------------------------------------------------------------

        /// <summary>Boostet den Handel: mehr Handelsvolumen, mehr Kontrakt-Gewinn, gratis Kontrakt-Unity.
        /// Alles-oder-nichts: schlaegt beim Aktivieren ein Teil fehl, werden die bereits gesetzten
        /// Modifier zurueckgerollt — sonst bliebe ein halber Boost aktiv, den die UI als "aus" zeigt.</summary>
        public void SetTradeBoost(bool enabled)
        {
            bool ok = true;
            ok &= ApplyPercent(IdsCore.PropertyIds.TradeVolumeMultiplier, enabled, TradeVolumeBoostPercent, "Handelsvolumen");
            ok &= ApplyPercent(IdsCore.PropertyIds.ContractsProfitMultiplier, enabled, TradeProfitBoostPercent, "Kontrakt-Gewinn");
            // Kontrakt-Unity-Kosten auf 0 (-100% Delta).
            ok &= ApplyPercent(IdsCore.PropertyIds.ContractsUnityCostMultiplier, enabled, -100, "Kontrakt-Unity-Kosten");
            if (!ok && enabled)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Handels-Boost nur teilweise anwendbar — rolle zurueck.");
                ApplyPercent(IdsCore.PropertyIds.TradeVolumeMultiplier, false, 0, "Handelsvolumen (Rollback)");
                ApplyPercent(IdsCore.PropertyIds.ContractsProfitMultiplier, false, 0, "Kontrakt-Gewinn (Rollback)");
                ApplyPercent(IdsCore.PropertyIds.ContractsUnityCostMultiplier, false, 0, "Kontrakt-Unity-Kosten (Rollback)");
            }
            if (ok) _tradeCached = enabled;
            Log.Info($"[{CompanySupplier.ModName}] Handels-Boost = {enabled} (ok={ok}).");
        }

        // -- Kern ---------------------------------------------------------------------------------

        private bool ApplyBool(PropertyId<bool> id, bool enabled, string label)
        {
            if (_propertiesDb == null) return false;
            try
            {
                IProperty<bool> prop = _propertiesDb.GetProperty(id);
                if (prop == null) { Log.Warning($"[{CompanySupplier.ModName}] {label}: Property nicht gefunden."); return false; }
                if (enabled) prop.AddOrSetModifier(ModifierOwner, true, PropertyModifiers.NO_GROUP);
                else prop.TryRemoveModifier(ModifierOwner);
                Log.Info($"[{CompanySupplier.ModName}] {label} = {enabled}.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] {label} umschalten: {ex.Message}");
                return false;
            }
        }

        private bool ApplyPercent(PropertyId<Percent> id, bool enabled, int deltaPercent, string label)
        {
            if (_propertiesDb == null) return false;
            try
            {
                IProperty<Percent> prop = _propertiesDb.GetProperty(id);
                if (prop == null) { Log.Warning($"[{CompanySupplier.ModName}] {label}: Property nicht gefunden."); return false; }
                if (enabled) prop.AddOrSetModifier(ModifierOwner, Percent.FromPercentVal(deltaPercent), PropertyModifiers.NO_GROUP);
                else prop.TryRemoveModifier(ModifierOwner);
                Log.Info($"[{CompanySupplier.ModName}] {label} Boost = {enabled} ({deltaPercent:+0;-0}%).");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] {label} umschalten: {ex.Message}");
                return false;
            }
        }
    }
}
