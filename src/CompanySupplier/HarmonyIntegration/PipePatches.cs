using HarmonyLib;
using Mafi.Core.Factory.Transports;

namespace CompanySupplier.HarmonyIntegration
{
    /// <summary>
    /// Harmony-Patch fuer „Rohre entlang Haengen bauen": entfernt vor der Wegsuche die Flat-Zwang-Flags aus
    /// den <see cref="TransportPathFinderOptions"/>, sodass Start-/Zielkachel nicht mehr eben sein muessen.
    ///
    /// Sauber und drift-arm umgesetzt: wir greifen NUR das public <c>Flags</c>-Feld der public Options-Struktur
    /// ab (kein privates Feld), gated ueber <see cref="PipeCheats.BuildAlongSlopes"/>. Ist der Cheat aus, ist
    /// der Prefix ein No-Op und das Original laeuft unveraendert.
    /// </summary>
    [HarmonyPatch(typeof(TransportPathFinder), "InitPathFinding")]
    internal static class Patch_TransportPathFinder_InitPathFinding
    {
        // TransportPathFinderOptions ist ein struct -> per ref modifizierbar, bevor das Original es liest.
        private static void Prefix(ref TransportPathFinderOptions options)
        {
            if (!PipeCheats.BuildAlongSlopes) return;

            TransportPathFinderFlags wanted =
                options.Flags & ~(TransportPathFinderFlags.StartMustBeFlat | TransportPathFinderFlags.GoalMustBeFlat);

            if (wanted != options.Flags)
            {
                options = new TransportPathFinderOptions(
                    options.PreferredHeight,
                    options.ForcedStartDirection,
                    options.BannedStartDirections,
                    wanted);
            }
        }
    }
}
