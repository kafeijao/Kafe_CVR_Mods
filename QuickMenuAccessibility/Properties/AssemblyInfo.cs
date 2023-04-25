using System.Reflection;
using Kafe.QuickMenuAccessibility;
using Kafe.QuickMenuAccessibility.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(QuickMenuAccessibility))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(QuickMenuAccessibility))]

[assembly: MelonInfo(
    typeof(QuickMenuAccessibility),
    nameof(Kafe.QuickMenuAccessibility),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(ConsoleColor.Green)]
[assembly: MelonAuthorColor(ConsoleColor.DarkYellow)]
[assembly: MelonAdditionalDependencies("BTKUILib")]
[assembly: MelonOptionalDependencies("ActionMenu", "MenuScalePatch")]

namespace Kafe.QuickMenuAccessibility.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.6";
    public const string Author = "kafeijao";
}
