using System.Reflection;
using Kafe.CVRSuperMario64;
using Kafe.CVRSuperMario64.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(CVRSuperMario64))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(CVRSuperMario64))]

[assembly: MelonInfo(
    typeof(Kafe.CVRSuperMario64.CVRSuperMario64),
    nameof(Kafe.CVRSuperMario64),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(255, 0, 255, 0)]
[assembly: MelonAuthorColor(255, 119, 77, 79)]
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName)]

namespace Kafe.CVRSuperMario64.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "1.0.3";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
}
