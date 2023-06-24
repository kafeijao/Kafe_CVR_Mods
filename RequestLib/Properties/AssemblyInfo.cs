using System.Reflection;
using Kafe.RequestLib;
using Kafe.RequestLib.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(RequestLib))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(RequestLib))]

[assembly: MelonInfo(
    typeof(RequestLib),
    nameof(Kafe.RequestLib),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(ConsoleColor.Green)]
[assembly: MelonAuthorColor(ConsoleColor.DarkYellow)]
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName)]
[assembly: MelonOptionalDependencies(AssemblyInfoParams.BTKSAImmersiveHudName)]

namespace Kafe.RequestLib.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.2";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
    public const string BTKSAImmersiveHudName = "BTKSAImmersiveHud";
}
