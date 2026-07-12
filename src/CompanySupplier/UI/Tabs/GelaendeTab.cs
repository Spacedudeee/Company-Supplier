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
using CompanySupplier.Localization;

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
        private IReadOnlyList<LooseProductProto> _materials;
        private Dropdown<LooseProductProto> _materialDropdown;

        private LooseProductProto _selectedMaterial;

        // T2 "Gelände-Physik deaktivieren" hat keine Backend-Status-Property -> lokal gehalten (Default aus).
        private bool _physicsDisabled;

        // Toggle-Referenzen + Suppress-Flag fuer den zentralen UI-Sync (CheatUiSync).
        private Toggle _physicsToggle, _towerToggle, _treeGrowthToggle;
        private bool _suppress;

        public GelaendeTab()
        {
            _materials = LoadMaterials();
            _selectedMaterial = _materials.Count > 0 ? _materials[0] : null;
            _content = BuildContent();
            CheatUiSync.Register(nameof(GelaendeTab), SyncFromState);
        }

        /// <summary>Zieht Toggle-Zustaende und Materialliste aus dem Backend nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try
            {
                _physicsToggle?.Value(_physicsDisabled);
                _towerToggle?.Value(CheatService.Instance?.Terrain?.IgnoreTowerDesignations ?? true);
                _treeGrowthToggle?.Value(CheatService.Instance?.Gameplay?.TreeGrowthBoost ?? false);
                RetryLoadMaterialsIfEmpty();
            }
            finally { _suppress = false; }
        }

        /// <summary>Recovery: war TerrainCheats beim Tab-Bau noch nicht verfuegbar, blieb das Dropdown leer.
        /// Beim naechsten Sync (z. B. Fensterbau) wird die Liste nachgeladen statt die ganze Session tot zu sein.</summary>
        private void RetryLoadMaterialsIfEmpty()
        {
            if (_materials.Count > 0 || _materialDropdown == null) return;
            var reloaded = LoadMaterials();
            if (reloaded.Count == 0) return;
            _materials = reloaded;
            _selectedMaterial = _materials[0];
            _materialDropdown.SetOptions(_materials);
            _materialDropdown.SetValueIndex(0, notifyChangeListeners: false);
            Log.Info($"[{CompanySupplier.ModName}] GelaendeTab: Materialliste nachgeladen ({_materials.Count}).");
        }

        public string Name => L.Tab_Gelaende;

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
            // p is IProtoWithIcon: die Dropdown-OptionFactory castet darauf — ein Proto ohne Icon
            // wuerde sonst beim Fensterbau eine InvalidCastException werfen (ganzes Menue tot).
            return terrain.GetTerrainMaterials()
                          .Where(p => p is IProtoWithIcon)
                          .OrderBy(p => CheatWidgets.ProtoDisplayName(p))
                          .ToList();
        }

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Gel_Material),
                BuildMaterialDropdown(),        // T1

                CheatWidgets.SectionTitle(L.Gel_TitleOptions),
                BuildPhysicsToggle(),           // T2
                BuildIgnoreTowerToggle(),       // T3

                CheatWidgets.SectionTitle(L.Gel_TitleMarkers),
                BuildInstantMarkerButtons(),    // T4 + T5 + T6

                CheatWidgets.SectionTitle(L.Gel_TitleReserves),
                BuildReserveButtons(),          // T7 + T8
                BuildTreeButtons(),             // T9 + T10
                BuildTreeGrowthToggle()         // T11
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
            _materialDropdown = dropdown;
            dropdown.Label(new LocStrFormatted(L.Gel_Material));
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
            _physicsToggle = CheatWidgets.NewToggleRow(
                L.Gel_Physics,
                _physicsDisabled,
                v =>
                {
                    if (_suppress) return;
                    _physicsDisabled = v;
                    CheatService.Instance?.Terrain?.SetTerrainPhysicsDisabled(v);
                },
                L.Gel_PhysicsTip);
            return _physicsToggle;
        }

        // T3: Turm-Markierungen ignorieren. Backend hat ein Status-Flag (IgnoreTowerDesignations,
        // Default an) -> Startzustand daraus seeden statt aus einem lokalen Default.
        private UiComponent BuildIgnoreTowerToggle()
        {
            bool initial = CheatService.Instance?.Terrain?.IgnoreTowerDesignations ?? true;
            _towerToggle = CheatWidgets.NewToggleRow(
                L.Gel_IgnoreTower,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Terrain?.SetIgnoreTowerDesignations(v); },
                L.Gel_IgnoreTowerTip);
            return _towerToggle;
        }

        // T4 + T5 + T6: drei Sofort-Aktions-Buttons auf die Markierungen, nutzen das T1-Material.
        private UiComponent BuildInstantMarkerButtons()
        {
            var mine = CheatWidgets.PrimaryButton(
                L.Gel_Mine,
                () => CheatService.Instance?.Terrain?.InstantMine(),
                L.Gel_MineTip);

            var dump = CheatWidgets.PrimaryButton(
                L.Gel_Dump,
                () =>
                {
                    if (_selectedMaterial != null)
                        CheatService.Instance?.Terrain?.InstantDump(_selectedMaterial);
                },
                L.Gel_DumpTip);

            var change = CheatWidgets.GeneralButton(
                L.Gel_Change,
                () =>
                {
                    if (_selectedMaterial != null)
                        CheatService.Instance?.Terrain?.ChangeTerrain(_selectedMaterial);
                },
                L.Gel_ChangeTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(mine, dump, change);
            return row;
        }

        // T7 + T8: Reserven auffuellen.
        private UiComponent BuildReserveButtons()
        {
            var water = CheatWidgets.PrimaryButton(
                L.Gel_FillWater,
                () => CheatService.Instance?.Terrain?.FillGroundWater(),
                L.Gel_FillWaterTip);

            var crude = CheatWidgets.PrimaryButton(
                L.Gel_FillCrude,
                () => CheatService.Instance?.Terrain?.FillGroundCrude(),
                L.Gel_FillCrudeTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(water, crude);
            return row;
        }

        // T9 + T10: Baeume pflanzen / entfernen.
        private UiComponent BuildTreeButtons()
        {
            var plant = CheatWidgets.PrimaryButton(
                L.Gel_PlantTrees,
                () => CheatService.Instance?.Terrain?.AddTrees(),
                L.Gel_PlantTreesTip);

            var remove = CheatWidgets.DangerButton(
                L.Gel_RemoveTrees,
                () => CheatService.Instance?.Terrain?.RemoveTrees(),
                L.Gel_RemoveTreesTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(plant, remove);
            return row;
        }

        // T11: Baum-Wachstum x10 (Dauer-Toggle ueber GameplayCheats, per CheatUiSync nachgezogen).
        private UiComponent BuildTreeGrowthToggle()
        {
            bool initial = CheatService.Instance?.Gameplay?.TreeGrowthBoost ?? false;
            _treeGrowthToggle = CheatWidgets.NewToggleRow(
                L.Gel_TreeGrowth,
                initial,
                v => { if (!_suppress) CheatService.Instance?.Gameplay?.SetTreeGrowthBoost(v); },
                L.Gel_TreeGrowthTip);
            return _treeGrowthToggle;
        }

    }
}
