namespace CompanySupplier.HarmonyIntegration
{
    /// <summary>
    /// Statische Schalter fuer die Harmony-basierten Pipe-Build-Cheats. Die Patches lesen diese Flags pro
    /// Aufruf — ein ausgeschalteter Cheat greift NICHT in die normale Bau-Logik ein (die Patches sind dann
    /// No-Ops und rufen das Original unveraendert).
    ///
    /// Aktuell umgesetzt: „Rohre entlang Haengen bauen" (Flat-Endpunkt-Zwang entfernen). Die drei weiteren
    /// Cheat++-Pipe-Cheats sind in 0.8.5.0 nicht sicher patchbar (siehe README/Doku): „unterirdisch" sitzt in
    /// der privaten Kollisionslogik des Pathfinders, „Bulldozer inkl. unterirdisch" hat keinen auffindbaren
    /// Controller, „Tiefenlabels" braeuchten ein eigenes Welt-Render-Overlay.
    /// </summary>
    internal static class PipeCheats
    {
        /// <summary>true = Transport-/Rohr-Endpunkte duerfen auf Haengen liegen
        /// (Pathfinder-Flags StartMustBeFlat/GoalMustBeFlat werden entfernt).</summary>
        public static bool BuildAlongSlopes;
    }
}
