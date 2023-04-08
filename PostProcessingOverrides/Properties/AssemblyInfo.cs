using System.Reflection;
using Kafe.PostProcessingOverrides;
using Kafe.PostProcessingOverrides.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(PostProcessingOverrides))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(PostProcessingOverrides))]

[assembly: MelonInfo(
    typeof(PostProcessingOverrides),
    nameof(Kafe.PostProcessingOverrides),
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

namespace Kafe.PostProcessingOverrides.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.2";
    public const string Author = "kafeijao";
}
