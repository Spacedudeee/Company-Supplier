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
        private readonly IReadOnlyList<ProductProto> _products;

        private ProductProto _selectedProduct;
        private int _quantity = QtyDefault;
        private Slider _qtySlider;
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

        public RessourcenTab()
        {
            _products = LoadProducts();
            _selectedProduct = _products.Count > 0 ? _products[0] : null;
            _content = BuildContent();
        }

        public string Name => "Ressourcen";

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
            return protos.Filter<ProductProto>(p => p.CanBeLoadedOnTruck)
                         .OrderBy(p => CheatWidgets.ProtoDisplayName(p))
                         .ToList();
        }

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Ressourcen ins Lager geben"),
                BuildProductDropdown(),     // R1
                BuildQuantityRow(),          // R2
                BuildActionButtons(),        // R3 + R4
                CheatWidgets.SectionTitle("Lager-Werkzeug"),
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
            dropdown.Label(new LocStrFormatted("Produkt"));
            // R3-Wunsch: Suchfeld oben im aufgeklappten Dropdown -> Produkte schnell finden.
            // Suche matcht sowohl den angezeigten dt. Namen als auch die (englische) Id -> beide Eingaben treffen.
            dropdown.SetSearchStringLookup((ProductProto proto) => CheatWidgets.ProtoDisplayName(proto) + " " + proto.Id.ToString());
            dropdown.SetOptions(_products);
            dropdown.OnValueChanged((ProductProto proto, int index) => _selectedProduct = proto);
            dropdown.FlexGrow(1f); // R2-Wunsch: volle Breite (Button so breit wie das aufgeklappte Menü)

            if (_products.Count > 0)
                dropdown.SetValueIndex(0, notifyChangeListeners: false);

            return dropdown;
        }

        // R2: Mengen-Slider (volle Breite, zeigt die absolute Menge live via ValueFormatter) + Stepper.
        private UiComponent BuildQuantityRow()
        {
            _qtySlider = new Slider()
                .Range(QtyMin, QtyMax)
                .Value(_quantity, notify: false)
                .Label(new LocStrFormatted("Menge"))
                .ValueFormatter(CheatWidgets.UnitFormatter(QtyMin, QtyMax, "Stück"));
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
            col.SetChildren(_qtySlider, stepper);
            return col;
        }

        /// <summary>Setzt die Menge (geklemmt 10..10000); der Slider zeigt sie via ValueFormatter live.</summary>
        private void SetQuantity(int value, bool updateSlider = true)
        {
            value = Math.Max(QtyMin, Math.Min(QtyMax, value));
            _quantity = value;
            if (updateSlider) _qtySlider?.Value(value, notify: false);
        }

        // R3 + R4: zwei Buttons nebeneinander.
        private UiComponent BuildActionButtons()
        {
            var addOne = CheatWidgets.PrimaryButton(
                "Produkt hinzufügen",
                () =>
                {
                    if (_selectedProduct != null)
                    {
                        CheatService.Instance?.GiveResource(_selectedProduct, _quantity);
                        CheatMenuStatus.Show($"{_quantity}x {CheatWidgets.ProtoDisplayName(_selectedProduct)} hinzugefügt");
                    }
                },
                "Legt die eingestellte Menge des gewählten Produkts ins zentrale Lager.");

            var addAll = CheatWidgets.GeneralButton(
                "ALLE Produkte hinzufügen",
                () =>
                {
                    CheatService.Instance?.GiveAllResources(_quantity);
                    CheatMenuStatus.Show($"{_quantity}x von ALLEN Produkten hinzugefügt");
                },
                "Legt die eingestellte Menge von JEDEM spawnbaren Produkt ins Lager.");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(addOne, addAll);
            return row;
        }

        // R5: ALLE-Lager-Gottmodus-Toggle. ACHTUNG: setzt JEDES Lager auf KeepFull (sofort komplett gefuellt +
        // dauerhaft voll), drastisch und nicht sauber rueckgaengig (Originalinhalt ueberschrieben). Daher
        // deutlich als Warnung beschriftet UND zwei-Klick-bestaetigt. Fuer gezielte EINZELNE Lager: R6 "Lager-Zauberstab".
        //
        // Zwei-Klick-Ablauf (nur beim EINSCHALTEN): erster Klick schaltet "scharf" und schnappt den Toggle sofort
        // wieder auf AUS zurueck (Value(false) feuert OnValueChanged NICHT erneut -> keine Rekursion), zweiter Klick
        // loest tatsaechlich aus. AUSSCHALTEN/None wirkt ohne Bestaetigung sofort; jeder Aus-Klick verwirft die
        // Scharfschaltung.
        private UiComponent BuildGodModeToggle()
        {
            _godModeToggle = CheatWidgets.NewToggleRow(
                "ALLE Lager füllen (Vorsicht!)",
                _storageGodMode,
                v =>
                {
                    if (v)
                    {
                        // EINSCHALTEN: erster Klick = nur scharfschalten, zweiter Klick loest aus.
                        if (!_godModeArmed)
                        {
                            _godModeArmed = true;
                            CheatMenuStatus.Show("Sicher? Nochmal klicken zum Bestätigen — füllt ALLE Lager.");
                            // Toggle wieder auf AUS, ohne den Callback erneut zu feuern.
                            _godModeToggle?.Value(false);
                            return;
                        }

                        // Zweiter Klick: ausfuehren.
                        _godModeArmed = false;
                        _storageGodMode = true;
                        CheatService.Instance?.Building?.SetAllStoragesGodMode(Storage.StorageCheatMode.KeepFull);
                        CheatMenuStatus.Show("ALLE Lager auf Gottmodus gesetzt (sofort komplett gefüllt)");
                    }
                    else
                    {
                        // AUSSCHALTEN: keine Bestaetigung; etwaige Scharfschaltung verfaellt.
                        _godModeArmed = false;
                        _storageGodMode = false;
                        CheatService.Instance?.Building?.SetAllStoragesGodMode(Storage.StorageCheatMode.None);
                        CheatMenuStatus.Show("Gottmodus für alle Lager AUS (Lagerinhalt bleibt)");
                    }
                },
                "VORSICHT: Setzt ALLE Lager im Spiel sofort auf 'immer voll' — jedes Lager wird komplett mit "
                + "seinem Produkt gefüllt und bleibt voll. Nicht sauber rückgängig (Originalinhalt überschrieben). "
                + "Zur Sicherheit zweistufig: erst klicken zum Scharfschalten, dann nochmal zum Auslösen. "
                + "Für einzelne Lager stattdessen den 'Lager-Zauberstab' (anklicken) nutzen.");
            return _godModeToggle;
        }

        // R6: Welt-Klick-Werkzeug FÜLLEN. Aktiv = Lager im Spiel anklicken schaltet dessen Gott-Modus (KeepFull) um.
        // Aktivierung laeuft ueber CheatService -> IUnityInputMgr.ActivateNewController(StorageWandController).
        private UiComponent BuildFillWandToggle()
        {
            _fillWandToggle = CheatWidgets.NewToggleRow(
                "Lager füllen (anklicken)",
                false,
                v => OnWandToggleChanged(Storage.StorageCheatMode.KeepFull, v),
                "Aktiv: ein einzelnes Lager im Spiel anklicken füllt es dauerhaft (KeepFull, klick = an/aus). "
                + "Nur EIN Lager-Werkzeug gleichzeitig — schaltet 'Lager leeren' automatisch ab.");
            return _fillWandToggle;
        }

        // R7: Welt-Klick-Werkzeug LEEREN. Aktiv = ein angeklicktes Lager wird EINMALIG geleert (Inhalt raus,
        // via Cheat_ForceClear) und laeuft danach NORMAL weiter — KEIN Dauer-Leer-Modus. Anwendungsfall z. B.
        // Atommuell. Das Werkzeug bleibt aktiv, sodass mehrere Lager nacheinander geleert werden koennen.
        private UiComponent BuildEmptyWandToggle()
        {
            _emptyWandToggle = CheatWidgets.NewToggleRow(
                "Lager leeren (anklicken)",
                false,
                v => OnWandToggleChanged(Storage.StorageCheatMode.KeepEmpty, v),
                "Aktiv: ein einzelnes Lager im Spiel anklicken leert es EINMALIG (Inhalt raus) — z. B. für "
                + "Atommüll. Das Lager läuft danach normal weiter (wird NICHT dauerhaft leer gehalten). "
                + "Nur EIN Lager-Werkzeug gleichzeitig — schaltet 'Lager füllen' automatisch ab.");
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
            string label = mode == Storage.StorageCheatMode.KeepEmpty ? "leeren" : "füllen";
            var otherToggle = mode == Storage.StorageCheatMode.KeepEmpty ? _fillWandToggle : _emptyWandToggle;

            if (on)
            {
                bool ok = CheatService.Instance?.SetStorageWandActive(true, mode) ?? false;
                if (ok)
                {
                    _activeWandMode = mode;
                    // Den anderen Toggle optisch abwaehlen (Value(false) feuert dessen Callback NICHT erneut,
                    // da OnValueChanged nur bei tatsaechlichem Wertwechsel auslost und wir hier auf false setzen).
                    otherToggle?.Value(false);
                    CheatMenuStatus.Show($"Lager {label} AN — ein Lager im Spiel anklicken");
                }
                else
                {
                    // DI-Teil fehlt: Toggle wieder zuruecksetzen, Zustand unveraendert lassen.
                    (mode == Storage.StorageCheatMode.KeepEmpty ? _emptyWandToggle : _fillWandToggle)?.Value(false);
                    CheatMenuStatus.Show($"Lager-Werkzeug ({label}) nicht verfügbar");
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
                    CheatMenuStatus.Show($"Lager {label} AUS");
                }
            }
        }

    }
}
