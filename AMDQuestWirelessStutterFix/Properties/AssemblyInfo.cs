using System.Reflection;
using Kafe.AMDQuestWirelessStutterFix;
using Kafe.AMDQuestWirelessStutterFix.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(AMDQuestWirelessStutterFix))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(AMDQuestWirelessStutterFix))]

[assembly: MelonInfo(
    typeof(AMDQuestWirelessStutterFix),
    nameof(Kafe.AMDQuestWirelessStutterFix),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame(null, "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: VerifyLoaderVersion(0, 6, 1, true)]
[assembly: MelonColor(255, 0, 255, 0)]
[assembly: MelonAuthorColor(255, 119, 77, 79)]
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName)]
[assembly: MelonAdditionalCredits(AssemblyInfoParams.Patchuuri)]

namespace Kafe.AMDQuestWirelessStutterFix.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.3";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
    public const string Patchuuri = "Patchuuri";
}
