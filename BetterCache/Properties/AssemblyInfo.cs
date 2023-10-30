using System.Reflection;
using Kafe.BetterCache;
using Kafe.BetterCache.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.BetterCache))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.BetterCache))]

[assembly: MelonInfo(
    typeof(BetterCache),
    nameof(Kafe.BetterCache),
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

namespace Kafe.BetterCache.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.3";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
}
