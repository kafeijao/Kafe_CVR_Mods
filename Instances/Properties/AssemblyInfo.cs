using System.Reflection;
using Kafe.Instances;
using Kafe.Instances.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Instances))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Instances))]

[assembly: MelonInfo(
    typeof(Instances),
    nameof(Kafe.Instances),
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
[assembly: MelonOptionalDependencies(AssemblyInfoParams.ChatBoxName)]

namespace Kafe.Instances.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "1.0.12";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
    public const string ChatBoxName = "ChatBox";
}
