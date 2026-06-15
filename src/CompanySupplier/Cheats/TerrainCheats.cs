using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Physics;
using Mafi.Core.Terrain.Trees;

namespace CompanySupplier.Cheats
{
    /// <summary>
    /// Gelaende-Cheats (ui-spec T1-T10): Physik-Toggle, Sofort-Abbau/-Verfuellen, Gelaende-Umwandeln,
    /// Grundwasser/Erdoel auffuellen, Baeume pflanzen/entfernen, plus Material-Helfer fuer das Dropdown.
    ///
    /// Robustheits-Prinzip (Early-Access-API kann driften): Manager via TryResolve, jeder Cheat in
    /// try/catch mit Log.Warning. Drift-resistent gehalten: Material-/Baum-/VirtualResource-Auswahl
    /// laeuft ueber Prototyp-Filter statt harter Id-Konstanten (die alten Ids.* / IdsCore.* Pfade
    /// liessen sich gegen 0.8.5.0 nicht mehr per Inspektor bestaetigen).
    /// </summary>
    public sealed class TerrainCheats
    {
        private readonly DependencyResolver _resolver;

        private ProtosDb _protos;
        private TerrainManager _terrainManager;
        private ITerrainMiningManager _miningManager;
        private ITerrainDumpingManager _dumpingManager;
        private ITreesManager _treesManager;
        private VirtualResourceManager _virtualResourceManager;

        /// <summary>T3 "Turm-Markierungen ignorieren" — Provider-internes Flag (es gibt keinen globalen
        /// Game-Toggle dafuer); die Instant-Operationen lesen es als Parameter. Default an (ui-spec T3).</summary>
        public bool IgnoreTowerDesignations { get; private set; } = true;

        public TerrainCheats(DependencyResolver resolver)
        {
            _resolver = resolver;
            _resolver.TryResolve<ProtosDb>(out _protos);
            _resolver.TryResolve<TerrainManager>(out _terrainManager);
            _resolver.TryResolve<ITerrainMiningManager>(out _miningManager);
            _resolver.TryResolve<ITerrainDumpingManager>(out _dumpingManager);
            _resolver.TryResolve<ITreesManager>(out _treesManager);
            _resolver.TryResolve<VirtualResourceManager>(out _virtualResourceManager);
        }

        // ------------------------------------------------------------------------------------------
        // T1 Helper: verfuegbare Materialien fuer das Dropdown
        // ------------------------------------------------------------------------------------------

        /// <summary>Liefert alle Schuettgueter, die auf einen LKW geladen werden koennen UND auf dem
        /// Gelaende liegen koennen — die spawn-/dump-baren Gelaende-Materialien fuer T4/T6 (ui-spec T1).</summary>
        public IReadOnlyList<LooseProductProto> GetTerrainMaterials()
        {
            if (_protos == null) return Array.Empty<LooseProductProto>();
            try
            {
                return _protos
                    .Filter<LooseProductProto>(p => p.CanBeLoadedOnTruck && p.CanBeOnTerrain)
                    .OrderBy(p => p.Id.ToString())
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] GetTerrainMaterials: {ex.Message}");
                return Array.Empty<LooseProductProto>();
            }
        }

        // ------------------------------------------------------------------------------------------
        // T2 Gelaende-Physik deaktivieren / T3 Turm-Markierungen ignorieren
        // ------------------------------------------------------------------------------------------

        /// <summary>T2: schaltet die Terrain-Physik- UND Disruption-Simulation ab/an. Die Simulatoren
        /// sind keine DI-Singletons, sondern private Felder des TerrainManager — sie werden per
        /// Reflection geholt und ueber ihr public SetDisabled(bool) umgeschaltet.</summary>
        public void SetTerrainPhysicsDisabled(bool disabled)
        {
            if (_terrainManager == null) return;
            try
            {
                SetSimulatorDisabled<ITerrainPhysicsSimulator>("m_physicsSimulator", disabled);
                SetSimulatorDisabled<ITerrainDisruptionSimulator>("m_terrainDisruptionSimulator", disabled);
                Log.Info($"[CompanySupplier] Gelaende-Physik deaktiviert = {disabled}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] SetTerrainPhysicsDisabled: {ex.Message}");
            }
        }

        private void SetSimulatorDisabled<TSim>(string fieldName, bool disabled) where TSim : class
        {
            // Das Feld ist private (verifiziert 0.8.5.0) -> per Reflection holen. Die Methode
            // SetDisabled(bool) ist auf beiden Simulator-Interfaces (ITerrainPhysicsSimulator /
            // ITerrainDisruptionSimulator) public (verifiziert 0.8.5.0) -> direkt ueber das gecastete
            // Interface aufrufen statt per Reflection (robuster, kein Laufzeit-Risiko).
            var field = typeof(TerrainManager).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Log.Warning($"[CompanySupplier] Simulator-Feld {fieldName} nicht gefunden (API-Drift?).");
                return;
            }
            if (field.GetValue(_terrainManager) is TSim sim)
            {
                CallSetDisabled(sim, disabled);
            }
            else
            {
                Log.Warning($"[CompanySupplier] Simulator-Feld {fieldName} ist null/nicht vom erwarteten Typ.");
            }
        }

        /// <summary>Ruft das public SetDisabled(bool) der beiden Simulator-Interfaces direkt auf
        /// (kein Reflection — verifiziert public in 0.8.5.0).</summary>
        private static void CallSetDisabled(object sim, bool disabled)
        {
            switch (sim)
            {
                case ITerrainPhysicsSimulator physics:
                    physics.SetDisabled(disabled);
                    break;
                case ITerrainDisruptionSimulator disruption:
                    disruption.SetDisabled(disabled);
                    break;
                default:
                    Log.Warning($"[CompanySupplier] CallSetDisabled: unbekannter Simulator-Typ {sim?.GetType().Name}.");
                    break;
            }
        }

        /// <summary>T3: Provider-internes Flag (siehe Property-Doku). Beeinflusst T4/T5.</summary>
        public void SetIgnoreTowerDesignations(bool ignore)
        {
            IgnoreTowerDesignations = ignore;
            Log.Info($"[CompanySupplier] Turm-Markierungen ignorieren = {ignore}.");
        }

        // ------------------------------------------------------------------------------------------
        // T4 Sofort abbauen
        // ------------------------------------------------------------------------------------------

        /// <summary>T4: setzt alle (nicht erfuellten) Abbau-Markierungen sofort auf ihre Zielhoehe.
        /// Respektiert T3 (Turm-Markierungen ignorieren).</summary>
        public void InstantMine()
        {
            if (_miningManager == null) return;
            try
            {
                var designations = _miningManager.MiningDesignations
                    .Where(d => d.IsNotFulfilled)
                    .ToList();

                int done = 0;
                foreach (var designation in designations)
                {
                    if (IgnoreTowerDesignations && !designation.ManagedByTowers.IsEmpty()) continue;
                    HarvestTreesIn(designation);
                    // Cheat_SetTerrainToMatch ist eine public Cheat-API direkt auf der Designation:
                    // setzt jede Kachel auf die Designations-Zielhoehe (Abbau ODER Verfuellen).
                    designation.Cheat_SetTerrainToMatch();
                    done++;
                }
                Log.Info($"[CompanySupplier] Sofort abbauen: {done} Markierungen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] InstantMine: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------
        // T5 Sofort verfuellen
        // ------------------------------------------------------------------------------------------

        /// <summary>T5: fuellt alle (nicht erfuellten) Verfuell-Markierungen sofort mit dem gewaehlten
        /// Material auf ihre Zielhoehe. Respektiert T3.</summary>
        public void InstantDump(LooseProductProto material)
        {
            if (_dumpingManager == null || _terrainManager == null || material == null) return;
            try
            {
                var thickness = ToTerrainThickness(material);

                var designations = _dumpingManager.DumpingDesignations
                    .Where(d => d.IsNotFulfilled)
                    .ToList();

                int done = 0;
                foreach (var designation in designations)
                {
                    if (IgnoreTowerDesignations && !designation.ManagedByTowers.IsEmpty()) continue;
                    HarvestTreesIn(designation);
                    designation.ForEachTile((TerrainTile tile, HeightTilesF f) =>
                    {
                        _terrainManager.DumpMaterialUpToHeight(tile.CoordAndIndex, thickness.AsSlim, f);
                    });
                    done++;
                }
                Log.Info($"[CompanySupplier] Sofort verfuellen ({material.Id}): {done} Markierungen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] InstantDump: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------
        // T6 Gelaende umwandeln
        // ------------------------------------------------------------------------------------------

        /// <summary>T6: wandelt die oberste Materialschicht ALLER Verfuell-Markierungen in das gewaehlte
        /// Material um (z. B. Erde fuer Farmen), ohne die Hoehe zu aendern. Respektiert T3.</summary>
        public void ChangeTerrain(LooseProductProto material)
        {
            if (_dumpingManager == null || _terrainManager == null || material == null) return;
            try
            {
                var thickness = ToTerrainThickness(material);
                if (thickness.Material == null)
                {
                    Log.Warning($"[CompanySupplier] ChangeTerrain: {material.Id} hat kein Gelaende-Material.");
                    return;
                }
                var slimId = thickness.Material.SlimId;

                var designations = _dumpingManager.DumpingDesignations.ToList();
                int done = 0;
                foreach (var designation in designations)
                {
                    if (IgnoreTowerDesignations && !designation.ManagedByTowers.IsEmpty()) continue;
                    designation.ForEachTile((TerrainTile tile, HeightTilesF f) =>
                    {
                        _terrainManager.ConvertMaterialInFirstLayer(
                            tile.CoordAndIndex, slimId, ThicknessTilesF.One, ThicknessTilesF.One);
                    });
                    done++;
                }
                Log.Info($"[CompanySupplier] Gelaende umwandeln ({material.Id}): {done} Markierungen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] ChangeTerrain: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------
        // T7 Grundwasser auffuellen / T8 Erdoel auffuellen
        // ------------------------------------------------------------------------------------------

        /// <summary>T7: fuellt alle Grundwasser-Reserven bis zur Kapazitaet auf.</summary>
        public void FillGroundWater() => RefillVirtualResource(new[] { "groundwater", "water" }, "Grundwasser");

        /// <summary>T8: fuellt alle Erdoel-Reserven bis zur Kapazitaet auf.</summary>
        public void FillGroundCrude() => RefillVirtualResource(new[] { "crude", "oil" }, "Erdoel");

        /// <summary>Findet die passende VirtualResource ueber Id-Substring (drift-resistent statt harter
        /// IdsCore.Products-Konstante) und fuellt alle ihre Vorkommen auf Kapazitaet.</summary>
        private void RefillVirtualResource(string[] idNeedles, string label)
        {
            if (_virtualResourceManager == null || _protos == null) return;
            try
            {
                var protos = _protos.Filter<VirtualResourceProductProto>(_ => true).ToList();
                VirtualResourceProductProto match = null;
                foreach (var needle in idNeedles)
                {
                    match = protos.FirstOrDefault(p =>
                        p.Id.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (match != null) break;
                }
                if (match == null)
                {
                    Log.Warning($"[CompanySupplier] {label}: keine VirtualResource gefunden " +
                                $"(gesucht: {string.Join("/", idNeedles)}).");
                    return;
                }

                var resources = _virtualResourceManager.GetAllResourcesFor(match);
                int count = 0;
                foreach (var resource in resources)
                {
                    resource.AddAsMuchAs(resource.Capacity);
                    count++;
                }
                Log.Info($"[CompanySupplier] {label} aufgefuellt ({match.Id}): {count} Vorkommen.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] {label} auffuellen: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------
        // T9 Baeume pflanzen / T10 Baeume entfernen
        // ------------------------------------------------------------------------------------------

        /// <summary>T9: pflanzt Baeume auf jede freie Kachel der Verfuell-Markierungen.</summary>
        public void AddTrees()
        {
            if (_dumpingManager == null || _treesManager == null) return;
            try
            {
                var treeProto = _protos?.Filter<TreeProto>(_ => true).FirstOrDefault();
                if (treeProto == null)
                {
                    Log.Warning("[CompanySupplier] AddTrees: kein TreeProto verfuegbar.");
                    return;
                }

                int planted = 0;
                foreach (var designation in _dumpingManager.DumpingDesignations.ToList())
                {
                    if (!designation.ManagedByTowers.IsEmpty()) continue;
                    designation.ForEachTile((TerrainTile tile, HeightTilesF f) =>
                    {
                        // TryPlantTree fuehrt die Spiel-eigene Validierung (Blockierung/Abstand/Ozean/
                        // Fruchtbarkeit) selbst durch und ist die public Komfort-API dafuer.
                        if (_treesManager.TryPlantTree(treeProto, tile.TileCoord)) planted++;
                    });
                }
                Log.Info($"[CompanySupplier] Baeume pflanzen: {planted} Baeume.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] AddTrees: {ex.Message}");
            }
        }

        /// <summary>T10: entfernt alle zur Entfernung (Ernte) markierten Baeume.</summary>
        public void RemoveTrees()
        {
            if (_treesManager == null) return;
            try
            {
                var treeIds = _treesManager.EnumerateSelectedTrees().ToList();
                int removed = 0;
                foreach (var id in treeIds)
                {
                    if (_treesManager.TryRemoveTree(id, skipAddingStump: true, collapseIt: false)) removed++;
                }
                Log.Info($"[CompanySupplier] Baeume entfernen: {removed} Baeume.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompanySupplier] RemoveTrees: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------
        // Hilfen
        // ------------------------------------------------------------------------------------------

        /// <summary>Wandelt ein Schuettgut in eine maximale Gelaende-Materialschicht (wie im Vorbild:
        /// LooseProductQuantity(MaxValue).ToTerrainThickness()).</summary>
        private static TerrainMaterialThickness ToTerrainThickness(LooseProductProto material)
        {
            var lpq = new LooseProductQuantity(material, Quantity.MaxValue);
            return lpq.ToTerrainThickness();
        }

        /// <summary>Erntet/entfernt vorhandene Baeume in einer Designation, damit Abbau/Verfuellen nicht
        /// blockiert wird.</summary>
        private void HarvestTreesIn(TerrainDesignation designation)
        {
            if (_treesManager == null) return;
            // EnumerateTreesInArea erwartet PolygonTerrainArea2i; designation.Area ist RectangleTerrainArea2i
            // -> ueber den verifizierten Konverter FromRectArea umwandeln.
            var area = PolygonTerrainArea2i.FromRectArea(designation.Area);
            var trees = _treesManager.EnumerateTreesInArea(area, null, null).ToList();
            foreach (var id in trees)
            {
                _treesManager.TryRemoveTree(id, skipAddingStump: true, collapseIt: false);
            }
        }
    }
}
