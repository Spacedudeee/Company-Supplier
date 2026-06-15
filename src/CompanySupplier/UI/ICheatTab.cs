using Mafi;
using Mafi.Unity.UiToolkit.Component;

namespace CompanySupplier.UI
{
    /// <summary>
    /// Vertrag fuer einen Reiter im Cheat-Menue. Analog zu <c>ICheatProviderTab</c> der Referenz,
    /// aber auf das 0.8.5.0-UiToolkit portiert: ein Tab IST eine <see cref="UiComponent"/> (typisch eine
    /// <c>Column</c>), die das Fenster ueber <c>TabContainer.AddTab(Name, this, IconPath, ...)</c> einhaengt.
    ///
    /// Alle Tab-Implementierungen tragen <c>[GlobalDependency(AsEverything)]</c> und werden dadurch
    /// automatisch vom DI-Container registriert; das Fenster zieht sie ueber
    /// <c>AllImplementationsOf&lt;ICheatTab&gt;</c> ein.
    ///
    /// WICHTIG: Das Interface MUSS <c>[MultiDependency]</c> tragen, damit der DI-Container
    /// <c>AllImplementationsOf&lt;ICheatTab&gt;</c> auflösen kann (sonst Laufzeit-Fehler beim Spielstart:
    /// "Failed to resolve all deps of 'ICheatTab' ... not marked with 'MultiDependencyAttribute'").
    /// </summary>
    [MultiDependency]
    public interface ICheatTab
    {
        /// <summary>Angezeigter Reiter-Name (deutsch, z. B. "Ressourcen").</summary>
        string Name { get; }

        /// <summary>Pfad zum Reiter-Icon (Spiel-Asset-Pfad, z. B.
        /// "Assets/Unity/UserInterface/Toolbar/Storages.svg"). Leerer String = kein Icon.</summary>
        string IconPath { get; }

        /// <summary>Der gerenderte Inhalt des Reiters (in 0.8.5.0 eine fertig aufgebaute UiComponent).</summary>
        UiComponent Content { get; }
    }
}
