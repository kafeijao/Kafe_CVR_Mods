using System.Reflection;
using Kafe.ChatBox;
using Kafe.ChatBox.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.ChatBox))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.ChatBox))]

[assembly: MelonInfo(
    typeof(ChatBox),
    nameof(Kafe.ChatBox),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(ConsoleColor.Green)]
[assembly: MelonAuthorColor(ConsoleColor.DarkYellow)]
[assembly: MelonOptionalDependencies(AssemblyInfoParams.BTKUILibName)]

namespace Kafe.ChatBox.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.2";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
}
