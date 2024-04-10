using System.Reflection;
using Kafe.NavMeshFollower;
using Kafe.NavMeshFollower.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.NavMeshFollower))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.NavMeshFollower))]

[assembly: MelonInfo(
    typeof(NavMeshFollower),
    nameof(Kafe.NavMeshFollower),
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
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName, AssemblyInfoParams.NavMeshToolsName, AssemblyInfoParams.RequestLibName)]

namespace Kafe.NavMeshFollower.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.6";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
    public const string NavMeshToolsName = "NavMeshTools";
    public const string RequestLibName = "RequestLib";
}
