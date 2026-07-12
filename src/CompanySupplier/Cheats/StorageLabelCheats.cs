using System;
using Mafi;
using Mafi.Core.Buildings.Storages;    // Storage, Storage.StorageCheatMode
using Mafi.Unity.Entities;             // EntitiesIconRenderer

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Cheat-Provider "Lager-Weltlabels": setzt ein schwebendes, eingefaerbtes Icon ueber ein Lager, das im
    /// Cheat-Modus ist (gelb = KeepFull, rot = KeepEmpty) — die Cheat++-„KF/KE"-Marker, hier als spielnative
    /// farbige Icons (echter Zwei-Buchstaben-Text braeuchte ein eigenes AssetBundle, siehe custom-ui-icons).
    ///
    /// Mechanik: der spieleigene <see cref="EntitiesIconRenderer"/> (Mafi.Unity) zeichnet Entity-Icons; er ist
    /// DI-registriert und wird lazy aufgeloest. <see cref="StorageToolCheats.SetCheatMode"/> ruft nach jedem
    /// Moduswechsel <see cref="UpdateLabel"/>. Icons sind reine Sicht (nicht serialisiert).
    ///
    /// Robustheit: alles gegen Unity-Renderer-State — jeder Aufruf null-guarded + try/catch + Log.Warning.
    /// </summary>
    public sealed class StorageLabelCheats
    {
        // Verifizierter Built-in-Asset-Pfad (wie die Tab-Icons); Farbe unterscheidet KF/KE.
        private const string IconPath = "Assets/Unity/UserInterface/Toolbar/Storages.svg";

        private readonly DependencyResolver _resolver;
        private EntitiesIconRenderer _renderer;
        private bool _init;

        // Zwei Icon-Specs (gelb/rot); lazy gebaut, sobald der Renderer aufgeloest ist.
        // EntitiesIconRenderer.AddIcon erwartet Mafi.Core.Gfx.IconSpec (svg-Path + Farbe + Scale).
        private Mafi.Core.Gfx.IconSpec _kfSpec;
        private Mafi.Core.Gfx.IconSpec _keSpec;

        public StorageLabelCheats(DependencyResolver resolver) => _resolver = resolver;

        private void EnsureInit()
        {
            if (_init) return;
            _init = true;
            try
            {
                _resolver.TryResolve<EntitiesIconRenderer>(out _renderer);
                _kfSpec = new Mafi.Core.Gfx.IconSpec(IconPath, ColorRgba.Yellow, 1f);
                _keSpec = new Mafi.Core.Gfx.IconSpec(IconPath, ColorRgba.Red, 1f);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] StorageLabelCheats init: {ex.Message}"); }
        }

        /// <summary>Setzt/entfernt das Weltlabel eines Lagers passend zu seinem Cheat-Modus. Entfernt vorher
        /// beide Farben (idempotent), fuegt dann bei KeepFull/KeepEmpty das passende hinzu.</summary>
        public void UpdateLabel(Storage storage, Storage.StorageCheatMode mode)
        {
            EnsureInit();
            if (_renderer == null || storage == null) return;
            SafeRemove(_kfSpec, storage);
            SafeRemove(_keSpec, storage);
            try
            {
                if (mode == Storage.StorageCheatMode.KeepFull)       _renderer.AddIcon(_kfSpec, storage);
                else if (mode == Storage.StorageCheatMode.KeepEmpty) _renderer.AddIcon(_keSpec, storage);
            }
            catch (Exception ex) { Log.Warning($"[{CompanySupplier.ModName}] UpdateLabel({storage?.Id}): {ex.Message}"); }
        }

        // RemoveIcon eines nicht gesetzten Icons darf keine Ausnahme nach oben werfen -> isoliert kapseln.
        private void SafeRemove(Mafi.Core.Gfx.IconSpec spec, Storage storage)
        {
            try { _renderer.RemoveIcon(spec, storage); }
            catch { /* Icon war nicht gesetzt — ignorieren */ }
        }
    }
}
