using System.Reflection;
using Kafe.CCK.Debugger.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(AssemblyInfoParams.Name)]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(AssemblyInfoParams.Name)]

[assembly: MelonInfo(
    typeof(Kafe.CCK.Debugger.CCKDebugger),
    AssemblyInfoParams.Name,
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: VerifyLoaderVersion(0, 6, 1, true)]
[assembly: MelonColor(255, 0, 255, 0)]
[assembly: MelonAuthorColor(255, 119, 77, 79)]
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName)]
[assembly: MelonAdditionalCredits(AssemblyInfoParams.AstroDoge)]

namespace Kafe.CCK.Debugger.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "2.0.6";
    public const string Author = "kafeijao";
    public const string AstroDoge = "AstroDoge";
    public const string Name = "CCK.Debugger";
    public const string BTKUILibName = "BTKUILib";
}
