﻿using System.Reflection;
using Kafe.TeleportRequest;
using Kafe.TeleportRequest.Properties;
using MelonLoader;


[assembly: AssemblyVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyFileVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyInformationalVersion(AssemblyInfoParams.Version)]
[assembly: AssemblyTitle(nameof(TeleportRequest))]
[assembly: AssemblyCompany(AssemblyInfoParams.Author)]
[assembly: AssemblyProduct(nameof(TeleportRequest))]

[assembly: MelonInfo(
    typeof(TeleportRequest),
    nameof(Kafe.TeleportRequest),
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
[assembly: MelonAdditionalDependencies(AssemblyInfoParams.RequestLibName, AssemblyInfoParams.BTKUILibName)]
[assembly: MelonOptionalDependencies(AssemblyInfoParams.ChatBoxName)]

namespace Kafe.TeleportRequest.Properties;
internal static class AssemblyInfoParams {
    public const string Version = "1.0.0";
    public const string Author = "kafeijao";
    public const string BTKUILibName = "BTKUILib";
    public const string RequestLibName = "RequestLib";
    public const string ChatBoxName = "ChatBox";
}
