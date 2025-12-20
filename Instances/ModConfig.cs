using System.Collections;
using System.Diagnostics;
using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Kafe.Instances;

public static class ModConfig
{
    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeRejoinLastInstanceOnGameRestart;
    internal static MelonPreferences_Entry<bool> MeRejoinPreviousLocation;

    internal static MelonPreferences_Entry<int> MeInstancesHistoryCount;

    internal static MelonPreferences_Entry<float> MeInstanceCreationJoinAttemptInterval;

    internal static MelonPreferences_Entry<int> MeJoiningLastInstanceMinutesTimeout;

    internal static MelonPreferences_Entry<bool> MePreventRejoiningEmptyInstances;

    internal static MelonPreferences_Entry<bool> MeAttemptToSaveAndLoadToken;

    public enum Region
    {
        Europe,
        UnitedStates,
    }

    public enum InstancePrivacyType
    {
        Public,
        FriendsOfFriends,
        Friends,
        EveryoneCanInvite,
        OwnerMustInvite,
    }

    private enum Icon
    {
        Logo,
        History,
        Privacy,
        Region,
        Restart,
        RestartDesktop,
        RestartVR,
    }

    private static string GetName(Icon icon)
    {
        switch (icon)
        {
            case Icon.Logo: return $"{nameof(Instances)}-Logo";
            case Icon.History: return $"{nameof(Instances)}-History";
            case Icon.Privacy: return $"{nameof(Instances)}-Privacy";
            case Icon.Region: return $"{nameof(Instances)}-Region";
            case Icon.Restart: return $"{nameof(Instances)}-Restart";
            case Icon.RestartDesktop: return $"{nameof(Instances)}-RestartDesktop";
            case Icon.RestartVR: return $"{nameof(Instances)}-RestartVR";
        }

        return "";
    }

    private const string VREnvArg = "-vr";

    private static bool _melonPrefsAlreadyInitialized = false;

    private static void UpdateFilePath(string userId)
    {
        // Use a diff file path so we can have per-user melon prefs. Requires to be ran after MetaPort.Instance.ownerId was set
        _melonCategory.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, $"MelonPreferences-{userId}.cfg"));
    }

    public static void InitializeOrUpdateMelonPrefs()
    {
        // If just updating the user, just needs to change target file
        if (_melonPrefsAlreadyInitialized)
        {
            UpdateFilePath(MetaPort.Instance.ownerId);
            return;
        }

        _melonPrefsAlreadyInitialized = true;

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(Instances));

        UpdateFilePath(MetaPort.Instance.ownerId);

        MeRejoinLastInstanceOnGameRestart = _melonCategory.CreateEntry("RejoinLastInstanceOnRestart", true,
            description: "Whether to join the last instance (if still available) when restarting the game or not.");

        MeInstancesHistoryCount = _melonCategory.CreateEntry("InstancesHistoryCount", 8,
            description: "How many instances should we keep on the history, needs to be between 4 and 24.");

        MeRejoinPreviousLocation = _melonCategory.CreateEntry("RejoinPreviousLocation", true,
            description: "Whether to teleport to previous location upon rejoining the last instance or not " +
                         $"(only works if rejoining within {Instances.TeleportToLocationTimeout} minutes.");

        MeInstanceCreationJoinAttemptInterval = _melonCategory.CreateEntry("InstanceCreationJoinAttemptInterval", 0.3f,
            description: "Time in seconds between attempts to join the instance created (defaults to 0.3 seconds).");

        MeJoiningLastInstanceMinutesTimeout = _melonCategory.CreateEntry("JoiningLastInstanceMinutesTimeout", 60,
            description:
            "For how many minutes should the game make you join the last instance. Use -1 to disable the timeout.");

        MePreventRejoiningEmptyInstances = _melonCategory.CreateEntry("PreventRejoiningEmptyInstances", true,
            description:
            "Whether to prevent joining empty instances or not. Empty instances might be closing already, " +
            "which will result in joining an instance and then getting disconnection spam.");

        MeAttemptToSaveAndLoadToken = _melonCategory.CreateEntry("AttemptToSaveAndLoadToken", true,
            description:
            "Whether to attempt to save current instance token when restarting and then use it to join. This " +
            "will allow rejoining private instances, but it might fail and cause you to not load into an offline world.");
    }


    // BTKUI Stuff

    public static void InitializeBTKUI()
    {
        QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager)
    {
        QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        var ass = Assembly.GetExecutingAssembly();
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.Logo),
            ass.GetManifestResourceStream("resources.BTKUILogoInstances.png"));
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.History),
            ass.GetManifestResourceStream("resources.BTKUIIconHistory.png"));
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.Privacy),
            ass.GetManifestResourceStream("resources.BTKUIIconPrivacy.png"));
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.Region),
            ass.GetManifestResourceStream("resources.BTKUIIconRegion.png"));
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.Restart),
            ass.GetManifestResourceStream("resources.BTKUIIconRestart.png"));
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.RestartDesktop),
            ass.GetManifestResourceStream("resources.BTKUIIconRestartDesktop.png"));
        QuickMenuAPI.PrepareIcon(nameof(Instances), GetName(Icon.RestartVR),
            ass.GetManifestResourceStream("resources.BTKUIIconRestartVR.png"));

        var page = new Page(nameof(Instances), nameof(Instances), true, GetName(Icon.Logo))
        {
            MenuTitle = nameof(Instances),
            MenuSubtitle = "Rejoin previous instances",
        };

        var categorySettings = page.AddCategory("");

        var restartButton = categorySettings.AddButton("Restart", GetName(Icon.Restart),
            "Restart in the current platform you're in currently.");
        restartButton.OnPress += () => MelonCoroutines.Start(RestartCVR(false));

        var joinLastInstanceButton = categorySettings.AddToggle("Join last instance after Restart",
            "Should we attempt to join the last instance you were in upon restarting the game? This takes " +
            "priority over starting in an Online Home World.",
            MeRejoinLastInstanceOnGameRestart.Value);
        joinLastInstanceButton.OnValueUpdated += b =>
        {
            if (b == MeRejoinLastInstanceOnGameRestart.Value) return;
            MeRejoinLastInstanceOnGameRestart.Value = b;
        };
        MeRejoinLastInstanceOnGameRestart.OnEntryValueChanged.Subscribe((_, newValue) =>
        {
            if (joinLastInstanceButton.ToggleValue == newValue) return;
            joinLastInstanceButton.ToggleValue = newValue;
        });

        var teleportToWhereWeLeft = categorySettings.AddToggle("Restart to same Location",
            "If you have Join Last Instance Enabled, we will also attempt to teleport to the previous location.",
            MeRejoinPreviousLocation.Value);
        teleportToWhereWeLeft.OnValueUpdated += b =>
        {
            if (b == MeRejoinPreviousLocation.Value) return;
            MeRejoinPreviousLocation.Value = b;
        };
        MeRejoinPreviousLocation.OnEntryValueChanged.Subscribe((_, newValue) =>
        {
            if (teleportToWhereWeLeft.ToggleValue == newValue) return;
            teleportToWhereWeLeft.ToggleValue = newValue;
        });

        var restartOtherPlatformButton = categorySettings.AddButton(
            $"Restart in {(MetaPort.Instance.isUsingVr ? "Desktop" : "VR")}",
            GetName(MetaPort.Instance.isUsingVr ? Icon.RestartDesktop : Icon.RestartVR),
            "Restart but switch the platform.");
        restartOtherPlatformButton.OnPress += () => MelonCoroutines.Start(RestartCVR(true));

        var configureHistoryLimit = categorySettings.AddButton("Set History Limit", GetName(Icon.History),
            "Define the number of instance to remember, needs to be between 4 and 24.");
        configureHistoryLimit.OnPress += () =>
        {
            QuickMenuAPI.OpenNumberInput("History Limit [4-24]", MeInstancesHistoryCount.Value,
                UpdateHistoryCount);
        };
        MeInstancesHistoryCount.OnEntryValueChanged.Subscribe((_, newValue) => UpdateHistoryCount(newValue));

        // Handle the recent instances
        var categoryInstances = page.AddCategory("Recent Instances");
        SetupRecentInstances(categoryInstances);
        Instances.RecentInstancesChanged += () => SetupRecentInstances(categoryInstances);
    }

    private static bool _isRestarting;

    internal static IEnumerator RestartCVR(bool switchPlatform)
    {
        if (_isRestarting)
        {
            MelonLogger.Warning("[CreateAndJoinOnlineInstance] Already restarting...");
            yield break;
        }

        _isRestarting = true;

        // If our token has less than 5 mins, grab a new instance token
        if (MeAttemptToSaveAndLoadToken.Value
            && Instances.PlayerConfig?.LastInstance?.JoinToken != null
            && Instances.PlayerConfig.LastInstance.JoinToken.ExpirationDate - DateTime.UtcNow < TimeSpan.FromMinutes(5))
        {
            MelonLogger.Msg($"Fetching a new instance token, since ours is pretty old or expired, Expire Date: " +
                            $"{Instances.PlayerConfig.LastInstance.JoinToken.ExpirationDate.ToLocalTime()}");
            var saveTokenTask = Instances.SaveCurrentInstanceToken();
            yield return new WaitUntil(() => saveTokenTask.IsCompleted);
        }

        try
        {
            var cvrExePath = Environment.GetCommandLineArgs()[0];
            var cvrArgs = "@()";
            var envArguments = Environment.GetCommandLineArgs().Skip(1).ToList();

            envArguments.Add(Instances.RestartedWithInstancesMod);

            // Handle platform switches
            if (switchPlatform)
            {
                if (envArguments.Contains(VREnvArg))
                    envArguments.Remove(VREnvArg);
                else
                    envArguments.Add(VREnvArg);
            }

            if (envArguments.Count > 0)
                cvrArgs = $"'{string.Join("', '", envArguments)}'";

            var powerShellLogFile = Path.GetFullPath(Path.Combine("UserData", nameof(Instances), Instances.InstancesPowerShellLog));

            // Get the process ID
            var processId = Process.GetCurrentProcess().Id;

            // Create the PowerShell script as a string
            var scriptContent = @"
            Start-Transcript -Path '" + powerShellLogFile + @"'
            # Wait for the process to stop running
            $processID = " + processId + @"
            $timeout = New-TimeSpan -Seconds 30
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            Write-Host ""Waiting for process with ID $processID to stop running...""
            do {
                $process = Get-Process -Id $processID -ErrorAction SilentlyContinue
                if ($process -ne $null) {
                    Write-Host ""Still waiting for process with ID $processID to stop running...""
                    Start-Sleep -Seconds 1
                }
            } while ($process -ne $null -and $sw.Elapsed -lt $timeout)

            # If the process stopped, start a new instance with arguments
            if ($process -eq $null) {
                $exePath = '" + cvrExePath + @"'
                $args = " + cvrArgs + @"
                Write-Host ""Starting $exePath with arguments: $args""
                Start-Process -FilePath $exePath -ArgumentList $args
                Start-Sleep -Seconds 2
            }
            else {
                Write-Host ""Process with ID $processID is still running. Timed out after $($timeout.TotalSeconds) seconds.""
                Write-Host ""Please make sure the process with ID $processID is not running before starting a new instance.""
                Start-Sleep -Seconds 10
            }
            Stop-Transcript
            ";

            // Execute the PowerShell script
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{scriptContent}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            var process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            Application.Quit();
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Attempted to restart the game, but something failed really bad :(");
            MelonLogger.Error(e);
        }
        finally
        {
            _isRestarting = false;
        }
    }

    private static void UpdateHistoryCount(float attemptedValue)
    {
        // Clamp possible values from 4 to 24 in multiples of 4
        var roundedValue = (int)Math.Round(attemptedValue / 4.0) * 4;
        roundedValue = Mathf.Clamp(roundedValue, 4, 24);

        if (MeInstancesHistoryCount.Value == roundedValue) return;

        MeInstancesHistoryCount.Value = roundedValue;

        // Clip the instance amount if necessary, and trigger save so it refreshes
        Instances.ApplyInstanceHistoryLimit(true);
    }

    private static void SetupRecentInstances(Category categoryInstances)
    {
        categoryInstances.ClearChildren();
        List<Instances.JsonConfigInstance> recentInstances = Instances.PlayerConfig.RecentInstances;
        for (var index = 0; index < recentInstances.Count; index++)
        {
            var instanceInfo = recentInstances[index];
            var button = categoryInstances.AddButton(
                $"{index}. {instanceInfo.InstanceName}",
                instanceInfo.WorldImageUrl,
                $"Join {instanceInfo.InstanceName}!",
                ButtonStyle.FullSizeImage);
            button.OnPress += () =>
            {
                if (instanceInfo.JoinToken != null)
                {
                    if (!MePreventRejoiningEmptyInstances.Value || instanceInfo.RemotePlayersCount > 0)
                    {
                        if (Instances.AttemptToUseTicked(instanceInfo)) return;
                    }
                    else
                    {
                        MelonLogger.Msg("Skipping rejoining using token, because the instance is probably " +
                                        "closing/closed. [MePreventRejoiningEmptyInstances=true]");
                    }
                }

                Instances.OnInstanceSelected(instanceInfo.InstanceId);
            };
        }
    }
}
