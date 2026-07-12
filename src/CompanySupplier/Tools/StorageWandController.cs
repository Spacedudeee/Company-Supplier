using System;
using Mafi;
using Mafi.Core.Buildings.Storages;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using UnityEngine;
using CompanySupplier.Cheats;
using CompanySupplier.Localization;

namespace CompanySupplier.Tools
{
    /// <summary>
    /// Welt-Klick-Werkzeug ("Lager-Zauberstab"): solange aktiv, wirkt ein Linksklick auf ein Lager je nach
    /// <see cref="TargetMode"/>: KeepFull = Gott-Modus (unendlich liefern) per Toggle an/aus; KeepEmpty =
    /// Lager EINMALIG leeren (Inhalt raus, danach normal weiter — KEIN Dauer-Leer-Modus). Der Tab waehlt
    /// ueber <see cref="TargetMode"/>, welches der beiden Werkzeuge (fuellen/leeren) aktiv ist — es gibt nur
    /// EINEN Controller, der zwischen beiden Modi umgeschaltet wird.
    ///
    /// 0.8.5.0-Weg (statt der gedrifteten <c>BaseEntityCursorInputController</c>-Vererbung der Referenz):
    /// schlanke Eigenimplementierung des winzigen <see cref="IUnityInputController"/> (Config/Activate/
    /// Deactivate/InputUpdate). Das Picking liefert der public <see cref="CursorPickingManager"/>
    /// (<c>TryPickEntity&lt;Storage&gt;</c>); das eigentliche Setzen laeuft ueber den
    /// <see cref="StorageToolCheats"/>-Provider (Reflection auf <c>Storage.Cheat_SetCheatMode</c>).
    ///
    /// Aktivierung/Deaktivierung laeuft ueber den <see cref="IUnityInputMgr"/>
    /// (<c>ActivateNewController</c>/<c>DeactivateController</c>), getriggert vom Ressourcen-Tab.
    ///
    /// Registrierung per <c>[GlobalDependency(AsEverything)]</c> -> der DI-Container baut den Controller
    /// und injiziert ihn; der Tab zieht ihn ueber <see cref="CheatService"/>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class StorageWandController : IUnityInputController
    {
        private readonly DependencyResolver _resolver;
        private readonly StorageToolCheats _tool;
        private CursorPickingManager _picker;
        private bool _pickerResolved;

        private bool _isActive;

        /// <summary>True, solange das Werkzeug aktiv ist (vom Menue-Toggle gespiegelt).</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Waehlt das Werkzeug: KeepFull (Default) = Gott-Modus per Klick toggeln (Ziel &lt;-&gt; None);
        /// KeepEmpty = angeklicktes Lager EINMALIG leeren (kein Dauer-Modus). Da nur EIN Controller existiert,
        /// schaltet das Setzen dieses Felds das Werkzeug zwischen Fuell-/Leer-Modus um.
        /// </summary>
        public Storage.StorageCheatMode TargetMode { get; set; } = Storage.StorageCheatMode.KeepFull;

        public StorageWandController(DependencyResolver resolver, StorageToolCheats tool)
        {
            _resolver = resolver;
            _tool = tool;
            // WICHTIG: KEIN resolver.TryResolve hier! Der Controller wird selbst gerade vom DI-Resolver
            // instanziiert — ein Resolve im Ctor ist ein REKURSIVER Resolve (Deadlock-Schutz wirft
            // "called resolve recursively"). Der CursorPickingManager wird daher lazy beim ersten
            // InputUpdate geholt (da ist die Instanziierung laengst abgeschlossen).
        }

        /// <summary>Holt den CursorPickingManager einmalig beim ersten Bedarf (lazy, robust). Null = inert.</summary>
        private CursorPickingManager Picker()
        {
            if (_pickerResolved) return _picker;
            _pickerResolved = true;
            _resolver.TryResolve<CursorPickingManager>(out _picker);
            if (_picker == null)
                Log.Warning($"[{CompanySupplier.ModName}] StorageWandController: CursorPickingManager nicht aufgeloest — Werkzeug inert.");
            return _picker;
        }

        /// <summary>
        /// Tool-Modus: blockt Bau-/Auswahl-Eingaben des Spiels, oeffnet aber kein eigenes Fenster.
        /// (<see cref="ControllerConfig.Tool"/> in 0.8.5.0 verifiziert.)
        /// </summary>
        public ControllerConfig Config => ControllerConfig.Tool;

        public void Activate()
        {
            _isActive = true;
            Log.Info($"[{CompanySupplier.ModName}] Lager-Zauberstab aktiviert (Lager anklicken zum Umschalten).");
        }

        public void Deactivate()
        {
            _isActive = false;
            Log.Info($"[{CompanySupplier.ModName}] Lager-Zauberstab deaktiviert.");
            // Auch der Input-Manager kann deaktivieren (z. B. wenn ein anderes Werkzeug/Bau-Tool
            // aktiviert wird) — die Menue-Toggles muessen das mitbekommen, sonst zeigen sie AN.
            UI.CheatUiSync.SyncAll();
        }

        /// <summary>
        /// Pro Frame (solange aktiv). Bei Linksklick auf ein Lager wird — je nach <see cref="TargetMode"/> —
        /// der Gott-Modus getoggelt (KeepFull) oder das Lager einmalig geleert (KeepEmpty). Rueckgabe
        /// <c>true</c> = Eingabe verbraucht (verhindert, dass der Klick zusaetzlich als Spiel-Auswahl gilt).
        /// </summary>
        public bool InputUpdate()
        {
            if (!_isActive) return false;
            var picker = Picker();
            if (picker == null) return false;
            if (!Input.GetMouseButtonDown(0)) return false;

            // Gekapselt wie beim GodWandController: InputUpdate laeuft im Frame-Loop des Spiels —
            // eine Exception aus Picking/Spiel-Interna darf dort niemals hochschlagen.
            try
            {
                if (picker.TryPickEntity<Storage>(out var storage) && storage != null)
                {
                    if (TargetMode == Storage.StorageCheatMode.KeepEmpty)
                    {
                        // "Leeren"-Werkzeug: Lager EINMALIG leeren (Inhalt raus), KEIN Dauer-Modus -> das Lager
                        // laeuft danach normal weiter (z. B. Atommuell entsorgen, ohne es dauerhaft leer zu halten).
                        _tool.ClearStorage(storage);
                        UI.CheatMenuStatus.Show(L.Wand_StorageEmptied(storage.Id));
                    }
                    else
                    {
                        // "Fuellen"-Werkzeug: Gott-Modus KeepFull <-> None toggeln (dauerhaft voll = unendlich
                        // liefern). ToggleMode liefert den TATSAECHLICH aktiven Modus (unveraendert bei Fehler).
                        var mode = _tool.ToggleMode(storage, TargetMode);
                        UI.CheatMenuStatus.Show(mode == Storage.StorageCheatMode.None
                            ? L.Wand_StorageNormal(storage.Id)
                            : L.Wand_StorageGodOn(storage.Id));
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[{CompanySupplier.ModName}] Lager-Zauberstab Klick: {ex.Message}");
            }

            return false;
        }
    }
}
