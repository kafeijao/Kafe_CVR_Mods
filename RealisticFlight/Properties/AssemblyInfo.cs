using System.Reflection;
using Kafe.RealisticFlight;
using Kafe.RealisticFlight.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(RealisticFlight))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(RealisticFlight))]

[assembly: MelonInfo(
    typeof(RealisticFlight),
    nameof(Kafe.RealisticFlight),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/kafeijao/Kafe_CVR_Mods"
)]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(ConsoleColor.Green)]
[assembly: MelonAuthorColor(ConsoleColor.DarkYellow)]
// [assembly: MelonAdditionalCredits(AssemblyInfoParams.SDraw)] Needs ML 6.1

namespace Kafe.RealisticFlight.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.3";
    public const string Author = "kafeijao";
    public const string SDraw = "SDraw";
}
