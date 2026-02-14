using System.Reflection;
using Kafe.MenuCSSLoader.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.MenuCSSLoader))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.MenuCSSLoader))]

[assembly: MelonInfo(
    typeof(Kafe.MenuCSSLoader.MenuCSSLoader),
    nameof(Kafe.MenuCSSLoader),
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
[assembly: MelonAdditionalCredits(AssemblyInfoParams.JillTheSomething)]

namespace Kafe.MenuCSSLoader.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "1.1.1";
    public const string Author = "kafeijao";
    public const string JillTheSomething = "JillTheSomething";
}
