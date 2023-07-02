using System.Reflection;
using Kafe.OSC.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.OSC))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.OSC))]

[assembly: MelonInfo(
    typeof(Kafe.OSC.OSC),
    nameof(Kafe.OSC),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(ConsoleColor.Green)]
[assembly: MelonAuthorColor(ConsoleColor.DarkYellow)]
[assembly: MelonOptionalDependencies(AssemblyInfoParams.ChatBoxName)]
[assembly: MelonIncompatibleAssemblies(AssemblyInfoParams.CVRParamLibName)]

namespace Kafe.OSC.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "1.1.2";
    public const string Author = "kafeijao";
    public const string ChatBoxName = "ChatBox";
    public const string CVRParamLibName = "CVRParamLib";
}
