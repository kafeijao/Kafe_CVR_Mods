﻿using System.Reflection;
using Kafe.RetroCVR;
using Kafe.RetroCVR.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(Kafe.RetroCVR))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(Kafe.RetroCVR))]

[assembly: MelonInfo(
    typeof(RetroCVR),
    nameof(Kafe.RetroCVR),
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
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.BTKUILibName)]

namespace Kafe.RetroCVR.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "0.0.2";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
}
