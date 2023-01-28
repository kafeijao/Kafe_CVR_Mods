using System.Reflection;
using EyeMovementFix.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(EyeMovementFix))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(EyeMovementFix))]

[assembly: MelonInfo(
    typeof(EyeMovementFix.EyeMovementFix),
    nameof(EyeMovementFix),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(ConsoleColor.Green)]
[assembly: MelonAuthorColor(ConsoleColor.DarkYellow)]
[assembly: MelonOptionalDependencies("PortableMirrorMod", "CCKDebugger")]

namespace EyeMovementFix.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "2.0.3";
    public const string Author = "kafeijao";
}
