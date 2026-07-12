using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Ressourcen" (ui-spec R1–R5): der Kern-Wunsch "alle Ressourcen geben".
    /// - R1 Produkt-Dropdown (alle auf-LKW-ladbaren Produkte, Default = erstes)
    /// - R2 Mengen-Slider (Min 10, Max 10000, Default 250) + Werte-Label
    /// - R3 Button "Produkt hinzufügen"  -> CheatService.GiveResource(proto, qty)
    /// - R4 Button "ALLE Produkte hinzufügen" -> CheatService.GiveAllResources(qty)
    /// - R5 Toggle "Lager-Gottmodus (liefern)" -> Building.SetAllStoragesGodMode(KeepFull/None)
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class RessourcenTab : ICheatTab
    {
        private const int QtyMin = 10;
        private const int QtyMax = 10000;
        private const int QtyDefault = 250;

        private readonly UiComponent _content;
        private IReadOnlyList<ProductProto> _products;
        private Dropdown<ProductProto> _productDropdown;

        private ProductProto _selectedProduct;
        private int _quantity = QtyDefault;
        private Slider _qtySlider;
        // Zeigt die eingestellte Menge als konkrete Zahl ("Menge: 250 Stück") — der Slider selbst
        // zeigt nur seine Position in Prozent, was die tatsaechliche Stueckzahl verdeckte.
        private Label _qtyLabel;
        // R5 "Lager-Gottmodus" hat keine Backend-Status-Property -> lokal gehalten.
        private bool _storageGodMode;
        // R5 Zwei-Klick-Bestaetigung: erster Klick schaltet "scharf", zweiter loest aus.
        private bool _godModeArmed;
        private Toggle _godModeToggle;
        // R6/R7 Welt-Klick-Werkzeug: nur EIN Modus gleichzeitig. null = aus; sonst KeepFull (fuellen)
        // bzw. KeepEmpty (leeren). Beide Toggles werden als Felder gehalten, um den jeweils anderen
        // beim Einschalten programmatisch optisch zurueckzusetzen (gegenseitiges Abwaehlen).
        private Storage.StorageCheatMode? _activeWandMode;
        private Toggle _fillWandToggle;
        private Toggle _emptyWandToggle;

        // Unterdrueckt die onChanged-Backend-Aufrufe, waehrend Toggles programmatisch gesetzt werden
        // (Sync, Zwei-Klick-Zuruecksetzen, gegenseitiges Abwaehlen). Gleiches Muster wie AllgemeinTab —
        // so ist das Verhalten unabhaengig davon, ob Toggle.Value(bool) den Callback feuert.
        private bool _suppress;

        public RessourcenTab()
        {
            _products = LoadProducts();
            _selectedProduct = _products.Count > 0 ? _products[0] : null;
            _content = BuildContent();
            CheatUiSync.Register(nameof(RessourcenTab), SyncFromState);
        }

        /// <summary>Zieht Werkzeug-Toggles und Produktliste aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                // Scharfschaltung der Zwei-Klick-Bestaetigung verfaellt beim Fensterbau/Sync: der
                // zugehoerige Statuszeilen-Hinweis ist dann laengst weg -> ein spaeterer EINZELNER Klick
                // darf nicht ungefragt ALLE Lager fuellen (nicht sauber rueckgaengig).
                _godModeArmed = false;
                var svc = CheatService.Instance;
                bool wandActive = svc?.IsStorageWandActive ?? false;
                var wandMode = svc?.StorageWandTargetMode ?? Storage.StorageCheatMode.None;
                _activeWandMode = wandActive ? (Storage.StorageCheatMode?)wandMode : null;
                _fillWandToggle?.Value(wandActive && wandMode == Storage.StorageCheatMode.KeepFull);
                _emptyWandToggle?.Value(wandActive && wandMode == Storage.StorageCheatMode.KeepEmpty);
                _godModeToggle?.Value(_storageGodMode);

                RetryLoadProductsIfEmpty();
            }
            finally { _suppress = false; }
        }

        /// <summary>Setzt einen Toggle-Wert programmatisch mit <c>_suppress</c>-Schutz (der Callback wird
        /// zum No-Op), try/finally-abgesichert — so bleibt <c>_suppress</c> nie haengen, falls
        /// <c>Toggle.Value</c> wirft (haette sonst alle Tab-Callbacks stumm geschaltet).</summary>
        private void SetToggleSuppressed(Toggle toggle, bool value)
        {
            _suppress = true;
            try { toggle?.Value(value); }
            finally { _suppress = false; }
        }

        /// <summary>Recovery: war die ProtosDb beim Tab-Bau noch nicht verfuegbar, blieb das Dropdown leer.
        /// Beim naechsten Sync (z. B. Fensterbau) wird die Liste nachgeladen statt die ganze Session tot zu sein.</summary>
        private void RetryLoadProductsIfEmpty()
        {
            if (_products.Count > 0 || _productDropdown == null) return;
            var reloaded = LoadProducts();
            if (reloaded.Count == 0) return;
            _products = reloaded;
            _selectedProduct = _products[0];
            _productDropdown.SetOptions(_products);
            _productDropdown.SetValueIndex(0, notifyChangeListeners: false);
            Log.Info($"[{CompanySupplier.ModName}] RessourcenTab: Produktliste nachgeladen ({_products.Count}).");
        }

        public string Name => L.Tab_Ressourcen;

        // Spiel-Asset-Pfad fuer das Lager-/Storages-Toolbar-Icon (Dateiname = EntityProto-Id).
        // String-Pfad ist in 0.8.5.0 die robuste Variante (kein IconStyle mehr); fehlt das Asset,
        // rendert der Tab trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Storages.svg";

        public UiComponent Content => _content;

        private IReadOnlyList<ProductProto> LoadProducts()
        {
            var protos = CheatService.Instance?.Protos;
            if (protos == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] RessourcenTab: ProtosDb nicht verfuegbar — Dropdown bleibt leer.");
                return Array.Empty<ProductProto>();
            }
            // p is IProtoWithIcon: die Dropdown-OptionFactory castet darauf — ein Proto ohne Icon
            // wuerde sonst beim Fensterbau eine InvalidCastException werfen (ganzes Menue tot).
            return protos.Filter<ProductProto>(p => p.CanBeLoadedOnTruck && p is IProtoWithIcon)
                         .OrderBy(p => CheatWidgets.ProtoDisplayName(p))
                         .ToList();
        }

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Res_TitleGive),
                BuildProductDropdown(),     // R1
                BuildQuantityRow(),          // R2
                BuildActionButtons(),        // R3 + R4
                CheatWidgets.SectionTitle(L.Res_TitleTool),
                BuildGodModeToggle(),        // R5: ALLE Lager auf einmal
                BuildFillWandToggle(),       // R6: einzelnes Lager fuellen (Welt-Klick)
                BuildEmptyWandToggle()       // R7: einzelnes Lager leeren (Welt-Klick)
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // R1: Produkt-Dropdown mit Icon + Name pro Option.
        private UiComponent BuildProductDropdown()
        {
            Dropdown<ProductProto>.OptionFactory factory =
                (ProductProto proto, int index, bool isInDropdown) =>
                    new ButtonIconText(Button.None, (IProtoWithIcon)proto, CheatWidgets.ProtoDisplayLabel(proto));

            var dropdown = new Dropdown<ProductProto>(
                factory,
                customButton: null,
                customBtnHolder: null,
                doNotUpdateBtnView: false);
            dropdown.Label(new LocStrFormatted(L.Res_Product));
            // R3-Wunsch: Suchfeld oben im aufgeklappten Dropdown -> Produkte schnell finden.
            // Suche matcht sowohl den angezeigten dt. Namen als auch die (englische) Id -> beide Eingaben treffen.
            dropdown.SetSearchStringLookup((ProductProto proto) => CheatWidgets.ProtoDisplayName(proto) + " " + proto.Id.ToString());
            dropdown.SetOptions(_products);
            dropdown.OnValueChanged((ProductProto proto, int index) => _selectedProduct = proto);
            dropdown.FlexGrow(1f); // R2-Wunsch: volle Breite (Button so breit wie das aufgeklappte Menü)

            if (_products.Count > 0)
                dropdown.SetValueIndex(0, notifyChangeListeners: false);

            _productDropdown = dropdown;
            return dropdown;
        }

        // R2: Mengen-Slider (volle Breite, zeigt die absolute Menge live via ValueFormatter) + Stepper.
        private UiComponent BuildQuantityRow()
        {
            // Konkrete Mengenanzeige ueber dem Regler (der Regler zeigt nur %).
            _qtyLabel = new Label(new LocStrFormatted(L.Res_QuantityValue(_quantity)));

            _qtySlider = new Slider()
                .Range(QtyMin, QtyMax)
                .Value(_quantity, notify: false)
                .Label(LocStrFormatted.Empty)  // Beschriftung liefert jetzt _qtyLabel ("Menge: N Stück")
                .ValueFormatter(CheatWidgets.UnitFormatter(QtyMin, QtyMax, L.Unit_Pieces));
            _qtySlider.OnValueChanged((OnSliderValueChanged)((oldValue, newValue) =>
                SetQuantity((int)Math.Round(newValue), updateSlider: false)));
            _qtySlider.FlexGrow(1f);

            // Slider allein ist bei 10–10000 zu grob -> Stepper fuer praezise Mengen.
            var stepper = CheatWidgets.NewIncrementButtonGroup(new Dictionary<int, Action<int>>
            {
                { 10,   d => SetQuantity(_quantity + d) },
                { 100,  d => SetQuantity(_quantity + d) },
                { 1000, d => SetQuantity(_quantity + d) },
            });

            var col = new Column((Px)CheatWidgets.Gap).AlignItemsStretch();
            col.SetChildren(_qtyLabel, _qtySlider, stepper);
            return col;
        }

        /// <summary>Setzt die Menge (geklemmt 10..10000); aktualisiert Slider-Position und die konkrete
        /// Mengenanzeige (_qtyLabel).</summary>
        private void SetQuantity(int value, bool updateSlider = true)
        {
            value = Math.Max(QtyMin, Math.Min(QtyMax, value));
            _quantity = value;
            if (updateSlider) _qtySlider?.Value(value, notify: false);
            if (_qtyLabel is IComponentWithText t) t.SetValue(new LocStrFormatted(L.Res_QuantityValue(value)));
        }

        // R3 + R4: zwei Buttons nebeneinander.
        private UiComponent BuildActionButtons()
        {
            var addOne = CheatWidgets.PrimaryButton(
                L.Res_AddProduct,
                () =>
                {
                    if (_selectedProduct != null)
                    {
                        CheatService.Instance?.GiveResource(_selectedProduct, _quantity);
                        CheatMenuStatus.Show(L.Res_StatusAdded(_quantity, CheatWidgets.ProtoDisplayName(_selectedProduct)));
                    }
                },
                L.Res_AddProductTip);

            var addAll = CheatWidgets.GeneralButton(
                L.Res_AddAll,
                () =>
                {
                    CheatService.Instance?.GiveAllResources(_quantity);
                    CheatMenuStatus.Show(L.Res_StatusAddedAll(_quantity));
                },
                L.Res_AddAllTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(addOne, addAll);
            return row;
        }

        // R5: ALLE-Lager-Gottmodus-Toggle. ACHTUNG: setzt JEDES Lager auf KeepFull (sofort komplett gefuellt +
        // dauerhaft voll), drastisch und nicht sauber rueckgaengig (Originalinhalt ueberschrieben). Daher
        // deutlich als Warnung beschriftet UND zwei-Klick-bestaetigt. Fuer gezielte EINZELNE Lager: R6 "Lager-Zauberstab".
        //
        // Zwei-Klick-Ablauf (nur beim EINSCHALTEN): erster Klick schaltet "scharf" und schnappt den Toggle sofort
        // wieder auf AUS zurueck, zweiter Klick loest tatsaechlich aus. Das programmatische Zuruecksetzen laeuft
        // _suppress-geschuetzt — so kann ein evtl. von Value(false) gefeuerter Callback die Scharfschaltung nicht
        // sofort wieder aufheben (frueher stillschweigend vorausgesetzt, jetzt explizit abgesichert).
        // AUSSCHALTEN/None wirkt ohne Bestaetigung sofort; jeder Aus-Klick verwirft die Scharfschaltung.
        private UiComponent BuildGodModeToggle()
        {
            _godModeToggle = CheatWidgets.NewToggleRow(
                L.Res_FillAll,
                _storageGodMode,
                v =>
                {
                    if (_suppress) return;
                    if (v)
                    {
                        // EINSCHALTEN: erster Klick = nur scharfschalten, zweiter Klick loest aus.
                        if (!_godModeArmed)
                        {
                            _godModeArmed = true;
                            CheatMenuStatus.Show(L.Res_StatusConfirm);
                            // Toggle wieder auf AUS — suppress-geschuetzt gegen Callback-Re-Entry.
                            SetToggleSuppressed(_godModeToggle, false);
                            return;
                        }

                        // Zweiter Klick: ausfuehren.
                        _godModeArmed = false;
                        _storageGodMode = true;
                        CheatService.Instance?.Building?.SetAllStoragesGodMode(Storage.StorageCheatMode.KeepFull);
                        CheatMenuStatus.Show(L.Res_StatusGodOn);
                    }
                    else
                    {
                        // AUSSCHALTEN: keine Bestaetigung; etwaige Scharfschaltung verfaellt.
                        _godModeArmed = false;
                        _storageGodMode = false;
                        CheatService.Instance?.Building?.SetAllStoragesGodMode(Storage.StorageCheatMode.None);
                        CheatMenuStatus.Show(L.Res_StatusGodOff);
                    }
                },
                L.Res_FillAllTip);
            return _godModeToggle;
        }

        // R6: Welt-Klick-Werkzeug FÜLLEN. Aktiv = Lager im Spiel anklicken schaltet dessen Gott-Modus (KeepFull) um.
        // Aktivierung laeuft ueber CheatService -> IUnityInputMgr.ActivateNewController(StorageWandController).
        private UiComponent BuildFillWandToggle()
        {
            _fillWandToggle = CheatWidgets.NewToggleRow(
                L.Res_FillWand,
                false,
                v => OnWandToggleChanged(Storage.StorageCheatMode.KeepFull, v),
                L.Res_FillWandTip);
            return _fillWandToggle;
        }

        // R7: Welt-Klick-Werkzeug LEEREN. Aktiv = ein angeklicktes Lager wird EINMALIG geleert (Inhalt raus,
        // via Cheat_ForceClear) und laeuft danach NORMAL weiter — KEIN Dauer-Leer-Modus. Anwendungsfall z. B.
        // Atommuell. Das Werkzeug bleibt aktiv, sodass mehrere Lager nacheinander geleert werden koennen.
        private UiComponent BuildEmptyWandToggle()
        {
            _emptyWandToggle = CheatWidgets.NewToggleRow(
                L.Res_EmptyWand,
                false,
                v => OnWandToggleChanged(Storage.StorageCheatMode.KeepEmpty, v),
                L.Res_EmptyWandTip);
            return _emptyWandToggle;
        }

        /// <summary>
        /// Gemeinsame Logik beider Wand-Toggles. Es existiert nur EIN Controller, daher ist gleichzeitig nur
        /// EIN Modus aktiv: beim Einschalten wird der Controller mit dem neuen Ziel-Modus (re-)aktiviert und der
        /// jeweils andere Toggle optisch auf false gesetzt. Beim Ausschalten wird das Werkzeug nur deaktiviert,
        /// wenn DIESER Modus der aktive war.
        /// </summary>
        private void OnWandToggleChanged(Storage.StorageCheatMode mode, bool on)
        {
            if (_suppress) return;
            // Ganze (lokalisierte) Saetze pro Modus statt ein interpoliertes Wort — sonst braeche die
            // Grammatik in anderen Sprachen ("Lager {leeren} AN" laesst sich nicht 1:1 uebersetzen).
            bool isEmpty = mode == Storage.StorageCheatMode.KeepEmpty;
            var otherToggle = isEmpty ? _fillWandToggle : _emptyWandToggle;

            if (on)
            {
                bool ok = CheatService.Instance?.SetStorageWandActive(true, mode) ?? false;
                if (ok)
                {
                    _activeWandMode = mode;
                    // Den anderen Toggle optisch abwaehlen — suppress-geschuetzt, damit ein evtl.
                    // gefeuerter Callback keinen Backend-Aufruf ausloest.
                    SetToggleSuppressed(otherToggle, false);
                    CheatMenuStatus.Show(isEmpty ? L.Res_StatusEmptyWandOn : L.Res_StatusFillWandOn);
                }
                else
                {
                    // DI-Teil fehlt: Toggle wieder zuruecksetzen, Zustand unveraendert lassen.
                    SetToggleSuppressed(isEmpty ? _emptyWandToggle : _fillWandToggle, false);
                    CheatMenuStatus.Show(isEmpty ? L.Res_StatusEmptyWandUnavail : L.Res_StatusFillWandUnavail);
                }
            }
            else
            {
                // Nur deaktivieren, wenn DIESER Modus gerade aktiv ist (sonst ist es das Abwaehlen durch den
                // anderen Toggle — Controller laeuft bereits korrekt im anderen Modus weiter).
                if (_activeWandMode == mode)
                {
                    CheatService.Instance?.SetStorageWandActive(false, mode);
                    _activeWandMode = null;
                    CheatMenuStatus.Show(isEmpty ? L.Res_StatusEmptyWandOff : L.Res_StatusFillWandOff);
                }
            }
        }

    }
}
