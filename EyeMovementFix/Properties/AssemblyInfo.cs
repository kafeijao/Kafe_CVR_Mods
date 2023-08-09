using System.Reflection;
using Kafe.EyeMovementFix.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.EyeMovementFix))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.EyeMovementFix))]

[assembly: MelonInfo(
    typeof(Kafe.EyeMovementFix.EyeMovementFix),
    nameof(Kafe.EyeMovementFix),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(255, 0, 255, 0)]
[assembly: MelonAuthorColor(255, 119, 77, 79)]
[assembly: MelonOptionalDependencies(AssemblyInfoParams.PortableMirrorModName, AssemblyInfoParams.CCKDebuggerName)]

namespace Kafe.EyeMovementFix.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "2.0.6";
    public const string Author = "kafeijao";
    public const string PortableMirrorModName = "PortableMirrorMod";
    public const string CCKDebuggerName = "CCKDebugger";
}
