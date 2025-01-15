using System.Reflection;
using Kafe.LoggerPlusPlus;
using Kafe.LoggerPlusPlus.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(AssemblyInfoParams.Name)]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(AssemblyInfoParams.Name)]

[assembly: MelonInfo(
    typeof(LoggerPlusPlus),
    AssemblyInfoParams.Name,
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: VerifyLoaderVersion(0, 6, 1, true)]
[assembly: MelonColor(255, 169, 169, 169)]
[assembly: MelonAuthorColor(255, 119, 77, 79)]

namespace Kafe.LoggerPlusPlus.Properties;
internal static class AssemblyInfoParams {
    public const string Name = "Logger++";
    public const string Version = "0.0.10";
    public const string Author = "kafeijao";
}
