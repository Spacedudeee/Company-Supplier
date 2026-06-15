using Mafi;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;

namespace CompanySupplier
{
    /// <summary>
    /// Einstiegspunkt des Cheat-Mods. Erbt von <see cref="DataOnlyMod"/> (implementiert das
    /// gesamte IMod-Boilerplate aus 0.8.5.0: Manifest/JsonConfig/Dispose) und ueberschreibt
    /// nur die fuer die Cheat-Engine relevanten Lifecycle-Methoden.
    ///
    /// P2-Skelett: beweist zunaechst nur, dass der Mod gegen 0.8.5.0 kompiliert, geladen wird
    /// und ins Log schreibt. Die Cheat-Provider werden in P3 in RegisterDependencies registriert.
    /// </summary>
    public sealed class CompanySupplier : DataOnlyMod
    {
        public const string ModName = "CompanySupplier";

        public CompanySupplier(ModManifest manifest) : base(manifest)
        {
            Log.Info($"[{ModName}] constructed (v0.1.0)");
        }

        /// <summary>Abstrakt in DataOnlyMod -> muss ueberschrieben werden. Cheat-Mod registriert
        /// (vorerst) keine eigenen Prototypen.</summary>
        public override void RegisterPrototypes(ProtoRegistrator registrator)
        {
        }

        /// <summary>In DataOnlyMod sind Initialize/RegisterDependencies 'sealed' — aber EarlyInit ist
        /// 'virtual' und liefert den DI-Resolver. Hier wird die Cheat-Engine eingehaengt.</summary>
        public override void EarlyInit(DependencyResolver resolver)
        {
            base.EarlyInit(resolver);
            CheatService.Create(resolver);
            Log.Info($"[{ModName}] EarlyInit abgeschlossen.");
        }
    }
}
