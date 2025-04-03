﻿using System.Reflection;
using Kafe.BetterPortals;
using Kafe.BetterPortals.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(BetterPortals))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(BetterPortals))]

[assembly: MelonInfo(
    typeof(BetterPortals),
    nameof(Kafe.BetterPortals),
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

namespace Kafe.BetterPortals.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.8";
    public const string Author = "kafeijao";
}
