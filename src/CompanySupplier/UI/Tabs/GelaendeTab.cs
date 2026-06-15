using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Gelände" (ui-spec T1–T10): der umfangreichste Tab — Material-Dropdown, zwei Toggles
    /// und sieben Aktions-Buttons. Alle Aktionen laufen ueber den Material-Kontext aus T1.
    /// - T1 Material-Dropdown (alle Gelaende-Schuettgueter, Default = erstes) -> Terrain.GetTerrainMaterials()
    /// - T2 Toggle "Gelände-Physik deaktivieren" (Default aus)  -> Terrain.SetTerrainPhysicsDisabled(bool)
    /// - T3 Toggle "Turm-Markierungen ignorieren" (Default an)  -> Terrain.SetIgnoreTowerDesignations(bool)
    /// - T4 Button "Sofort abbauen"        -> Terrain.InstantMine()
    /// - T5 Button "Sofort verfüllen"      -> Terrain.InstantDump(selectedMaterial)
    /// - T6 Button "Gelände umwandeln"     -> Terrain.ChangeTerrain(selectedMaterial)
    /// - T7 Button "Grundwasser auffüllen" -> Terrain.FillGroundWater()
    /// - T8 Button "Erdöl auffüllen"       -> Terrain.FillGroundCrude()
    /// - T9 Button "Bäume pflanzen"        -> Terrain.AddTrees()
    /// - T10 Button "Bäume entfernen"      -> Terrain.RemoveTrees()
    ///
    /// Bindet ueber <c>CheatService.Instance</c> (kein Provider-Injection). Registrierung per
    /// <c>[GlobalDependency(AsEverything)]</c> -> automatisch vom DI-Container gefunden.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class GelaendeTab : ICheatTab
    {
        private readonly UiComponent _content;
        private readonly IReadOnlyList<LooseProductProto> _materials;

        private LooseProductProto _selectedMaterial;

        // T2 "Gelände-Physik deaktivieren" hat keine Backend-Status-Property -> lokal gehalten (Default aus).
        private bool _physicsDisabled;

        public GelaendeTab()
        {
            _materials = LoadMaterials();
            _selectedMaterial = _materials.Count > 0 ? _materials[0] : null;
            _content = BuildContent();
        }

        public string Name => "Gelände";

        // "Mining"-Toolbar-Icon (Gelaende). Verifizierter Const-Pfad aus Mafi.Unity.Assets
        // (Toolbar.Mining_svg). String-Pfad ist in 0.8.5.0 die robuste Variante (kein IconStyle
        // mehr); fehlt das Asset, rendert der Tab trotzdem (nur ohne Icon).
        public string IconPath => "Assets/Unity/UserInterface/Toolbar/Mining.svg";

        public UiComponent Content => _content;

        private IReadOnlyList<LooseProductProto> LoadMaterials()
        {
            var terrain = CheatService.Instance?.Terrain;
            if (terrain == null)
            {
                Log.Warning($"[{CompanySupplier.ModName}] GelaendeTab: TerrainCheats nicht verfuegbar — Material-Dropdown bleibt leer.");
                return Array.Empty<LooseProductProto>();
            }
            // Nach dem lokalisierten Anzeigenamen sortieren -> Dropdown-Reihenfolge passt zur Beschriftung.
            return terrain.GetTerrainMaterials()
                          .OrderBy(p => CheatWidgets.ProtoDisplayName(p))
                          .ToList();
        }

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Material"),
                BuildMaterialDropdown(),        // T1

                CheatWidgets.SectionTitle("Optionen"),
                BuildPhysicsToggle(),           // T2
                BuildIgnoreTowerToggle(),       // T3

                CheatWidgets.SectionTitle("Markierungen sofort ausführen"),
                BuildInstantMarkerButtons(),    // T4 + T5 + T6

                CheatWidgets.SectionTitle("Reserven & Bäume"),
                BuildReserveButtons(),          // T7 + T8
                BuildTreeButtons()              // T9 + T10
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // T1: Material-Dropdown (Icon + Name pro Option), analog zum Produkt-Dropdown im Ressourcen-Tab.
        private UiComponent BuildMaterialDropdown()
        {
            Dropdown<LooseProductProto>.OptionFactory factory =
                (LooseProductProto proto, int index, bool isInDropdown) =>
                    new ButtonIconText(Button.None, (IProtoWithIcon)proto, CheatWidgets.ProtoDisplayLabel(proto));

            var dropdown = new Dropdown<LooseProductProto>(
                factory,
                customButton: null,
                customBtnHolder: null,
                doNotUpdateBtnView: false);
            dropdown.Label(new LocStrFormatted("Material"));
            // Suche matcht sowohl den angezeigten dt. Namen als auch die (englische) Id.
            dropdown.SetSearchStringLookup((LooseProductProto proto) => CheatWidgets.ProtoDisplayName(proto) + " " + proto.Id.ToString());
            dropdown.SetOptions(_materials);
            dropdown.OnValueChanged((LooseProductProto proto, int index) => _selectedMaterial = proto);
            dropdown.FlexGrow(1f);

            if (_materials.Count > 0)
                dropdown.SetValueIndex(0, notifyChangeListeners: false);

            return dropdown;
        }

        // T2: Gelaende-Physik global an/aus. Aktiv = Physik AUS (scharfe Kanten beim Abbau/Verfuellen).
        private UiComponent BuildPhysicsToggle()
        {
            return CheatWidgets.NewToggleRow(
                "Gelände-Physik deaktivieren (aktiv = AUS)",
                _physicsDisabled,
                v =>
                {
                    _physicsDisabled = v;
                    CheatService.Instance?.Terrain?.SetTerrainPhysicsDisabled(v);
                },
                "Aktiv = keine Physik-Simulation bei Abbau/Verfüllen (scharfe Kanten).");
        }

        // T3: Turm-Markierungen ignorieren. Backend hat ein Status-Flag (IgnoreTowerDesignations,
        // Default an) -> Startzustand daraus seeden statt aus einem lokalen Default.
        private UiComponent BuildIgnoreTowerToggle()
        {
            bool initial = CheatService.Instance?.Terrain?.IgnoreTowerDesignations ?? true;
            return CheatWidgets.NewToggleRow(
                "Turm-Markierungen ignorieren",
                initial,
                v => CheatService.Instance?.Terrain?.SetIgnoreTowerDesignations(v),
                "Aktiv = von Minen-Türmen verwaltete Markierungen werden bei Sofort-Operationen übersprungen.");
        }

        // T4 + T5 + T6: drei Sofort-Aktions-Buttons auf die Markierungen, nutzen das T1-Material.
        private UiComponent BuildInstantMarkerButtons()
        {
            var mine = CheatWidgets.PrimaryButton(
                "Sofort abbauen",
                () => CheatService.Instance?.Terrain?.InstantMine(),
                "Schließt alle Abbau-Markierungen sofort ab (nutzt Physik-/Turm-Optionen).");

            var dump = CheatWidgets.PrimaryButton(
                "Sofort verfüllen",
                () =>
                {
                    if (_selectedMaterial != null)
                        CheatService.Instance?.Terrain?.InstantDump(_selectedMaterial);
                },
                "Füllt alle Verfüll-Markierungen sofort mit dem gewählten Material.");

            var change = CheatWidgets.GeneralButton(
                "Gelände umwandeln",
                () =>
                {
                    if (_selectedMaterial != null)
                        CheatService.Instance?.Terrain?.ChangeTerrain(_selectedMaterial);
                },
                "Wandelt die oberste Materialschicht der Markierungen in das gewählte Material um (z. B. Erde für Farmen).");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(mine, dump, change);
            return row;
        }

        // T7 + T8: Reserven auffuellen.
        private UiComponent BuildReserveButtons()
        {
            var water = CheatWidgets.PrimaryButton(
                "Grundwasser auffüllen",
                () => CheatService.Instance?.Terrain?.FillGroundWater(),
                "Füllt alle Grundwasser-Reserven bis zur Kapazität auf.");

            var crude = CheatWidgets.PrimaryButton(
                "Erdöl auffüllen",
                () => CheatService.Instance?.Terrain?.FillGroundCrude(),
                "Füllt alle Erdöl-Reserven bis zur Kapazität auf.");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(water, crude);
            return row;
        }

        // T9 + T10: Baeume pflanzen / entfernen.
        private UiComponent BuildTreeButtons()
        {
            var plant = CheatWidgets.PrimaryButton(
                "Bäume pflanzen",
                () => CheatService.Instance?.Terrain?.AddTrees(),
                "Pflanzt Bäume in den Verfüll-Markierungen.");

            var remove = CheatWidgets.DangerButton(
                "Bäume entfernen",
                () => CheatService.Instance?.Terrain?.RemoveTrees(),
                "Entfernt alle zur Entfernung markierten Bäume.");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(plant, remove);
            return row;
        }

    }
}
