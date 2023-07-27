using System.Reflection;
using Kafe.FuckRTSP;
using Kafe.FuckRTSP.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(FuckRTSP))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(FuckRTSP))]

[assembly: MelonInfo(
    typeof(FuckRTSP),
    nameof(Kafe.FuckRTSP),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(255, 0, 255, 0)]
[assembly: MelonAuthorColor(255, 128, 128, 0)]
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName)]

namespace Kafe.FuckRTSP.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.2";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
}
