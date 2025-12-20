using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using Kafe.NavMeshFollower.Behaviors;
using Kafe.NavMeshFollower.Integrations;
using Kafe.NavMeshFollower.InteractableWrappers;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshFollower;

public static class ModConfig
{
    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeBakeNavMeshEverytimeFollowerSpawned;

    public static void InitializeMelonPrefs()
    {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(NavMeshFollower));

        MeBakeNavMeshEverytimeFollowerSpawned = _melonCategory.CreateEntry("BakeNavMeshEverytimeFollowerSpawned", false,
            description: "Whether to bake the nav mesh every time you spawn a follower or not. If not it will be " +
                         "generated the first time you spawn a follower in a world.");
    }

    public static void InitializeBTKUI()
    {
        QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static Page _pickupPage;
    private static Category _pickupSpawnableCategory;
    private static Category _pickupObjectSyncCategory;

    private static Action<Pickups.PickupWrapper> _onPickupSelected;

    private static void PromptPickup(Page currentPage, Action<Pickups.PickupWrapper> callback)
    {
        _onPickupSelected = pickupWrapper =>
        {
            callback?.Invoke(pickupWrapper);
            currentPage.OpenPage();
        };
        _pickupPage.OpenPage();
    }

    internal static void UpdatePickupList()
    {
        if (_pickupSpawnableCategory == null || PlayerSetup.Instance == null) return;

        _pickupSpawnableCategory.ClearChildren();
        var sortedSpawnablePickups = Pickups.AvailableSpawnablePickups.OrderBy(p =>
            Vector3.Distance(p.transform.position, PlayerSetup.Instance.GetPlayerPosition()));
        foreach (var spawnablePickup in sortedSpawnablePickups)
        {
            string guid = spawnablePickup.Spawnable.guid;
            bool gotImage = FollowerImages.TryGetPropImageUrl(guid, out string url);
            var button = _pickupSpawnableCategory.AddButton("", url, "Select this spawnable pickup");
            if (!gotImage) FollowerImages.SetFollowerButtonImage(button, guid);
            button.OnPress += () => _onPickupSelected?.Invoke(spawnablePickup);
        }

        _pickupObjectSyncCategory.ClearChildren();
        var sortedObjectSyncPickups = Pickups.AvailableObjectSyncPickups.OrderBy(p =>
            Vector3.Distance(p.transform.position, PlayerSetup.Instance.GetPlayerPosition()));
        foreach (var objectSyncPickup in sortedObjectSyncPickups)
        {
            var button =
                _pickupObjectSyncCategory.AddButton(objectSyncPickup.objectSync.name, "", "Select this world pickup");
            button.OnPress += () => _onPickupSelected?.Invoke(objectSyncPickup);
        }
    }

    private static Page _mainPage;
    private static Category _mainCategory;

    internal static void UpdateMainPage()
    {
        if (_mainCategory == null)
            return;

        _mainCategory.ClearChildren();

        foreach (var controller in FollowerController.FollowerControllers)
        {
            string guid = controller.Spawnable.guid;
            bool gotImage = FollowerImages.TryGetPropImageUrl(guid, out string url);
            var button = _mainCategory.AddButton(controller.LastHandledBehavior.GetStatus(), url, "Control this follower.");
            if (!gotImage) FollowerImages.SetFollowerButtonImage(button, guid);
            button.OnPress += () =>
            {
                _selectedFollowerController = controller;
                _followerControllerPage.OpenPage();
            };
        }
    }

    internal static void UpdatePlayerPage()
    {
        if (CVRPlayerManager.Instance == null ||
            !CVRPlayerManager.Instance.NetworkPlayers.Exists(p => p.Uuid == QuickMenuAPI.SelectedPlayerID))
            return;
        UpdatePlayerPage(QuickMenuAPI.SelectedPlayerName, QuickMenuAPI.SelectedPlayerID);
    }

    private static Button _requestLibButton;

    private static void UpdatePlayerPage(string playerName, string playerID)
    {
        if (_playersNavMeshCat == null) return;

        // Update the all follow target button
        _startFollowingTargetButton.ButtonText = $"All Follow {playerName}";
        _startFollowingTargetButton.ButtonTooltip = $"Make all followers follow {playerName}.";

        // Delete RequestLib button
        _requestLibButton?.Delete();

        var hasPermission = true;
        if (!NavMeshFollower.TestMode && !Friends.FriendsWith(playerID) &&
            !RequestLibIntegration.HasPermission(playerID))
        {
            hasPermission = false;

            if (RequestLibIntegration.HasRequestLib(playerID))
            {
                if (RequestLibIntegration.IsRequestPending(playerID))
                {
                    _requestLibButton = _playersNavMeshCat.AddButton("Waiting...", "", "Waiting for the Reply....");
                    _requestLibButton.Disabled = true;
                }
                else
                {
                    _requestLibButton = _playersNavMeshCat.AddButton("Request Permission", "",
                        $"Requests {playerName} permission to interact using RequestLib. It will last until the {playerName} leaves the instance.");
                    _requestLibButton.OnPress += () =>
                    {
                        RequestLibIntegration.RequestToInteract(playerName, playerID);
                        UpdatePlayerPage();
                    };
                }
            }
        }

        _startFollowingTargetButton.Disabled = !hasPermission;
        if (hasPermission)
        {
            _startFollowingTargetButton.OnPress += () =>
            {
                foreach (var followPlayerInstance in FollowPlayer.FollowPlayerInstances)
                {
                    followPlayerInstance.SetTarget(playerID);
                }

                UpdateMainPage();
                UpdatePlayerPage();
            };
        }
        else
        {
            _startFollowingTargetButton.ButtonText = "No Permission";
            _startFollowingTargetButton.ButtonTooltip = $"You can't send the props to interact with {playerName} " +
                                                        $"because they're not your friends and they haven't given " +
                                                        $"you permission via RequestLib.";
            _startFollowingTargetButton.OnPress += () =>
            {
                MelonLogger.Warning($"You don't have permission from {playerName} to let the props interact with them...");
            };
        }

        // Restore the advanced page
        _playersNavMeshAdvancedPageCat.ClearChildren();
        _playersNavMeshAdvancedPageCat.CategoryName = hasPermission ? "No Permission" : "Advanced";

        if (!hasPermission) return;

        foreach (var controller in FollowerController.FollowerControllers)
        {
            string guid = controller.Spawnable.guid;
            bool gotImage = FollowerImages.TryGetPropImageUrl(guid, out string url);
            var button = _playersNavMeshAdvancedPageCat.AddButton(controller.LastHandledBehavior.GetStatus(), url, "Control this follower.");
            if (!gotImage) FollowerImages.SetFollowerButtonImage(button, guid);
            button.OnPress += () =>
            {
                _selectedFollowerController = controller;
                _followerControllerPage.OpenPage();
            };
        }
    }

    private static Category _playersNavMeshCat;
    private static Page _playersNavMeshAdvancedPage;
    private static Category _playersNavMeshAdvancedPageCat;
    private static Button _stopFollowingButton;
    private static Button _startFollowingMeButton;
    private static Button _startFollowingTargetButton;


    private static Page _followerControllerPage;
    private static Category _followerControllerCategory;
    private static Category _followerControllerBehaviorsCategory;
    private static FollowerController _selectedFollowerController;

    internal static void UpdateFollowerControllerPage()
    {
        _followerControllerCategory.ClearChildren();
        if (_selectedFollowerController == null) return;

        var reBakeButton =
            _followerControllerCategory.AddButton("Re-Bake Nav Mesh", "", "Re-Bakes the Nav Mesh for this agent.");
        reBakeButton.OnPress += () =>
        {
            NavMeshTools.API.BakeCurrentWorldNavMesh(_selectedFollowerController.BakeAgent,
                (_, _) =>
                {
                    MelonLogger.Msg(
                        $"Finished baking the NavMeshData for {_selectedFollowerController.Spawnable.guid}");
                }, true);
            MelonLogger.Msg($"Re-Baking the Nav Mesh for {_selectedFollowerController.Spawnable.guid}");
            QuickMenuAPI.ShowNotice("Re-Bake Request",
                "Request to bake the nav mesh for the current agent was queued!");
        };

        _followerControllerBehaviorsCategory.ClearChildren();
        foreach (var behavior in _selectedFollowerController.Behaviors)
        {
            if (!behavior.IsToggleable) continue;
            var behaviorToggle = _followerControllerBehaviorsCategory.AddToggle(behavior.GetStatus(),
                behavior.Description, behavior.IsEnabled());
            behaviorToggle.OnValueUpdated += isOn =>
            {
                // Disable all other behaviors
                behavior.DisableAllBehaviorsExcept(behavior);

                switch (behavior)
                {
                    case FetchPickup fetchPickup:
                        if (isOn)
                        {
                            PromptPickup(_followerControllerPage,
                                pickupWrapper =>
                                {
                                    fetchPickup.FetchPickupTo(pickupWrapper, _currentMenuTargetPlayerGuid);
                                });
                        }
                        else
                        {
                            fetchPickup.FinishFetch();
                        }

                        break;

                    case PlayFetch playFetch:
                        if (isOn)
                        {
                            PromptPickup(_followerControllerPage,
                                pickupWrapper => { playFetch.StartPlayingFetch(pickupWrapper); });
                        }
                        else
                        {
                            playFetch.StopPlayingFetch();
                        }

                        break;

                    case FollowPlayer followPlayer:
                        if (isOn)
                        {
                            followPlayer.SetTarget(_currentMenuTargetPlayerGuid);
                        }
                        else
                        {
                            followPlayer.ClearTarget();
                        }

                        break;
                }

                UpdateFollowerControllerPage();
            };
        }
    }

    private static string _currentMenuTargetPlayerGuid;

    private static void SetupBTKUI(CVR_MenuManager manager)
    {
        QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // Create the Main Menu
        _mainPage = new Page(nameof(NavMeshFollower), nameof(NavMeshFollower), true, "")
        {
            MenuTitle = nameof(NavMeshFollower),
            MenuSubtitle = "Choose the follower you want to interact with.",
        };
        _mainCategory = _mainPage.AddCategory("");

        // Follower Controller Page
        // _followerControllerPage = _mainCategory.AddPage(nameof(NavMeshFollower) + " Controller", "", "", nameof(NavMeshFollower));
        _followerControllerPage =
            Page.GetOrCreatePage(nameof(NavMeshFollower), nameof(NavMeshFollower) + " Controller");
        _followerControllerPage.MenuTitle = nameof(NavMeshFollower) + " Controller";
        _followerControllerPage.MenuSubtitle = "Control the follower.";
        _followerControllerPage.Disabled = true;
        _followerControllerCategory = _followerControllerPage.AddCategory("General");
        _followerControllerBehaviorsCategory = _followerControllerPage.AddCategory("Behaviors");

        // Pickup Selector Page
        // _pickupPage = _followerControllerCategory.AddPage(nameof(NavMeshFollower) + " Pickup Selector", "", "", nameof(NavMeshFollower));
        _pickupPage =
            Page.GetOrCreatePage(nameof(NavMeshFollower),
                nameof(NavMeshFollower) + " Pickup Selector");
        _pickupPage.MenuTitle = nameof(NavMeshFollower) + " Pickup Selector";
        _pickupPage.MenuSubtitle = "Chose the pickup you want to interact with.";
        _pickupPage.Disabled = true;
        _pickupSpawnableCategory = _pickupPage.AddCategory("Props");
        _pickupObjectSyncCategory = _pickupPage.AddCategory("World Pickups");

        // Create the Player Selection Menu
        _playersNavMeshCat =
            QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(NavMeshFollower), nameof(NavMeshFollower));

        _stopFollowingButton = _playersNavMeshCat.AddButton("Stop all", "",
            "Stops all followers from following whoever they're following.");
        _stopFollowingButton.OnPress += () =>
        {
            foreach (var followPlayerInstance in FollowPlayer.FollowPlayerInstances)
            {
                followPlayerInstance.ClearTarget();
            }

            UpdateMainPage();
            UpdatePlayerPage();
        };

        _startFollowingMeButton = _playersNavMeshCat.AddButton("All Follow me", "", "Make all followers follow me.");
        _startFollowingMeButton.OnPress += () =>
        {
            foreach (var followPlayerInstance in FollowPlayer.FollowPlayerInstances)
            {
                followPlayerInstance.SetTarget(MetaPort.Instance.ownerId);
            }

            UpdateMainPage();
            UpdatePlayerPage();
        };

        _startFollowingTargetButton = _playersNavMeshCat.AddButton("All Follow X", "", "Make all followers follow X.");

        _playersNavMeshAdvancedPage =
            Page.GetOrCreatePage(nameof(NavMeshFollower), nameof(NavMeshFollower) + " Advanced");
        _playersNavMeshCat.AddButton("Advanced", "", "Advanced following targeting").OnPress +=
            () => { _playersNavMeshAdvancedPage.OpenPage(); };
        // _playersNavMeshAdvancedPage = _playersNavMeshCat.AddPage("Advanced", "", "Advanced following targeting", nameof(NavMeshFollower));
        _playersNavMeshAdvancedPageCat = _playersNavMeshAdvancedPage.AddCategory("");

        QuickMenuAPI.OnPlayerSelected += UpdatePlayerPage;

        // When a player is selected, set them as target.
        QuickMenuAPI.OnPlayerSelected += (_, playerGuid) =>
        {
            _currentMenuTargetPlayerGuid = playerGuid;
            _selectedFollowerController = null;
        };

        // Handle triggers when pages get opened
        QuickMenuAPI.OnOpenedPage += (openedElementId, lastElementId) =>
        {
            if (openedElementId == _mainPage.ElementID)
            {
                // When the main page is opened the local player is set as target
                _currentMenuTargetPlayerGuid = MetaPort.Instance.ownerId;
                _selectedFollowerController = null;
                UpdateMainPage();
            }
            else if (openedElementId == _pickupPage.ElementID) UpdatePickupList();
            else if (openedElementId == _followerControllerPage.ElementID) UpdateFollowerControllerPage();
        };
    }
}
