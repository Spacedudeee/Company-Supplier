using System;
using Mafi;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;

namespace CompanySupplier.UI
{
    /// <summary>
    /// Steuert das Cheat-Fenster: registriert den F8-Hotkey und oeffnet/schliesst (toggelt) das Fenster.
    ///
    /// 0.8.5.0-Portierung (die April-2024-Referenz ist komplett veraltet):
    /// - Basis ist <see cref="WindowController{TWindow}"/> aus <c>Mafi.Unity.InputControl</c> (frueher
    ///   <c>BaseWindowController</c> aus dem entfernten <c>Mafi.Unity.UiFramework</c>).
    /// - Der Ctor nimmt per DI <see cref="ControllerContext"/> (buendelt InputManager/GameLoop/UiRoot/Resolver)
    ///   sowie die <see cref="CheatMenuWindowView"/>, die das <see cref="Window"/> baut.
    /// - <see cref="CreateWindow"/> liefert das vom View gebaute Fenster — die Basis oeffnet/schliesst es.
    /// - Der F8-Hotkey laeuft ueber <c>IUnityInputMgr.RegisterGlobalShortcut(Func&lt;KeyBindings&gt;, this)</c>.
    ///
    /// Toolbar-Button: In 0.8.5.0 gibt es keinen mod-tauglichen oeffentlichen Weg mehr, einen Button in die
    /// Spiel-Toolbar zu haengen (<c>ToolbarController.AddMainMenuButton</c> entfernt; <c>ControllerToolbarMenuItem</c>
    /// hat einen internen Ctor). Daher ist F8 der primaere Trigger; der Toolbar-Button ist zurueckgestellt.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class CheatMenuController : WindowController<Window>
    {
        private readonly CheatMenuWindowView _view;
        private readonly IUnityInputMgr _inputMgr;
        private bool _shortcutRegistered;
        // Auto-Restore wird einmalig beim ersten Menue-Oeffnen nach einem Spielstand-Laden angewendet.
        // Der Controller wird pro Laden neu gebaut (DI laeuft je EarlyInit) -> Flag resettet sich natuerlich.
        private bool _autoRestoreApplied;

        public CheatMenuController(ControllerContext context, CheatMenuWindowView view)
            : base(context, ControllerConfig.Window)
        {
            _view = view;
            _inputMgr = context.InputManager;
            RegisterHotkey();
            Log.Info($"[{CompanySupplier.ModName}] CheatMenuController bereit (Hotkey F8).");
        }

        /// <summary>Wird von der Basis beim Aktivieren aufgerufen — baut das Fenster ueber den View.
        /// Beim ERSTEN Oeffnen nach einem Laden wird (falls aktiviert) der gespeicherte Cheat-Zustand
        /// automatisch wiederhergestellt.</summary>
        protected override Window CreateWindow()
        {
            TryAutoRestoreOnce();
            return _view.BuildWindow();
        }

        /// <summary>Wendet den gespeicherten Cheat-Zustand einmalig an, wenn Auto-Restore aktiv ist und
        /// etwas gespeichert wurde. Best-effort — bei jedem Fehler nur Log, nie Absturz.</summary>
        private void TryAutoRestoreOnce()
        {
            if (_autoRestoreApplied) return;
            _autoRestoreApplied = true;
            try
            {
                var svc = CheatService.Instance;
                if (svc?.Config?.AutoRestore == true && svc.Config.Toggles != null && svc.Config.Toggles.Count > 0)
                {
                    svc.ApplyState(svc.Config.Toggles);
                    CheatMenuStatus.Show("Gespeicherter Cheat-Zustand wiederhergestellt");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Auto-Restore fehlgeschlagen: {ex.Message}");
            }
        }

        private void RegisterHotkey()
        {
            if (_shortcutRegistered || _inputMgr == null) return;
            try
            {
                // F8 oeffnet/toggelt diesen Controller. Kategorie "Tools" + Game-Modus = im laufenden Spiel aktiv.
                // Die Shortcut-Factory bekommt in 0.8.5.0 einen ShortcutsManager und liefert die Bindung.
                _inputMgr.RegisterGlobalShortcut(
                    (Func<ShortcutsManager, KeyBindings>)(_ => KeyBindings.FromKey(KbCategory.Tools, ShortcutMode.Game, KeyCode.F8)),
                    this);
                _shortcutRegistered = true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] F8-Hotkey-Registrierung fehlgeschlagen: {ex.Message}");
            }
        }
    }
}
