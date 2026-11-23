using System.Reflection;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.PropManagement;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using ABI.CCK.Components;
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

    private static Page _mainPage;
    private static Category _followersCategory;
    private static Page _followerPage;
    private static FollowerController _selectedFollower;
    private static Page _pickupPage;
    private static Category _propPickupsCategory;
    private static Category _worldPickupsCategory;
    private static Action<Pickups.PickupWrapper> _onPickupChosen;

    public static void SetupBTKUI(Assembly assembly)
    {
        const string logoName = $"{nameof(NavMeshFollower)}-Logo";
        QuickMenuAPI.PrepareIcon(nameof(NavMeshFollower), logoName, assembly.GetManifestResourceStream("resources.logo.png"));

        // Main page
        _mainPage = new Page(nameof(NavMeshFollower), nameof(NavMeshFollower), true, logoName)
        {
            MenuTitle = nameof(NavMeshFollower),
            MenuSubtitle = "Pick a follower to control it, or command all of them at once",
        };
        _mainPage.OnVisibilityChanged += (isVisible, _) => { if (isVisible) RefreshMainPage(); };
        _followersCategory = _mainPage.AddCategory("Followers");

        var allCategory = _mainPage.AddCategory("All Followers");
        allCategory.AddButton("All Follow Me", "", "Makes all followers follow you").OnPress += () =>
        {
            foreach (var controller in FollowerController.FollowerControllers)
                Follow(controller, MetaPort.Instance.ownerId);
            RefreshMenus();
        };
        allCategory.AddButton("All Follow Player", "", "Pick a player for all followers to follow").OnPress += () =>
        {
            QuickMenuAPI.OpenPlayerSelector("Who should they follow?", player => RunIfAllowed(player, () =>
            {
                foreach (var controller in FollowerController.FollowerControllers)
                    Follow(controller, player.Uuid);
            }));
        };
        allCategory.AddButton("Stop All", "", "Makes all followers stop").OnPress += () =>
        {
            foreach (var controller in FollowerController.FollowerControllers)
                Stop(controller);
            RefreshMenus();
        };

        // Follower page
        _followerPage = Page.GetOrCreatePage(nameof(NavMeshFollower), "Follower");
        _followerPage.OnVisibilityChanged += (isVisible, _) => { if (isVisible) RefreshFollowerPage(); };

        var followCategory = _followerPage.AddCategory("Follow");
        followCategory.AddButton("Follow Me", "", "Makes this follower follow you").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            Follow(controller, MetaPort.Instance.ownerId);
            RefreshMenus();
        };
        followCategory.AddButton("Follow Player", "", "Pick a player to follow").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            QuickMenuAPI.OpenPlayerSelector("Who should it follow?",
                player => RunIfAllowed(player, () => Follow(controller, player.Uuid)));
        };

        var fetchCategory = _followerPage.AddCategory("Fetch");
        fetchCategory.AddButton("Fetch for Me", "", "Pick a pickup for this follower bring to you").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            ChoosePickup(pickup =>
            {
                QuickMenuAPI.GoBack();
                FetchFor(controller, pickup, MetaPort.Instance.ownerId);
            });
        };
        fetchCategory.AddButton("Fetch for Player", "", "Pick a pickup and a player to deliver it").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            ChoosePickup(pickup => QuickMenuAPI.OpenPlayerSelector("Who should get it?", player =>
            {
                // The player list goes back on its own, this one closes the pickup page too
                QuickMenuAPI.GoBack();
                RunIfAllowed(player, () => FetchFor(controller, pickup, player.Uuid));
            }));
        };
        fetchCategory.AddButton("Play Fetch", "", "Pick a pickup to play fetch with. Throw it and the follower brings it back").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            ChoosePickup(pickup =>
            {
                QuickMenuAPI.GoBack();
                PlayFetchWith(controller, pickup);
            });
        };

        var otherCategory = _followerPage.AddCategory("Other");
        otherCategory.AddButton("Stop", "", "Makes this follower stop what it's doing").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            Stop(controller);
            RefreshMenus();
        };
        otherCategory.AddButton("Re-Bake Nav Mesh", "", "Re-calculates the nav mesh").OnPress += () =>
        {
            if (!TryGetSelectedFollower(out var controller)) return;
            var guid = controller.Spawnable.guid;
            MelonLogger.Msg($"Re-Baking the Nav Mesh for {guid}");
            NavMeshTools.API.BakeCurrentWorldNavMesh(controller.BakeAgent,
                (_, _) => MelonLogger.Msg($"Finished baking the NavMeshData for {guid}"), true);
            QuickMenuAPI.ShowAlertToast("Re-baking the nav mesh for this follower...", 3);
        };

        // Pickup page
        _pickupPage = Page.GetOrCreatePage(nameof(NavMeshFollower), "Choose a Pickup");
        _pickupPage.OnVisibilityChanged += (isVisible, _) =>
        {
            if (isVisible)
            {
                RefreshPickupPage();
                return;
            }

            // Don't keep a long list around while nobody is looking at it
            _propPickupsCategory.ClearChildren();
            _worldPickupsCategory.ClearChildren();
        };
        _propPickupsCategory = _pickupPage.AddCategory("Props");
        _worldPickupsCategory = _pickupPage.AddCategory("World Pickups");
    }

    /// <summary>
    /// Updates the follower menus that are currently open
    /// </summary>
    internal static void RefreshMenus()
    {
        RefreshMainPage();
        RefreshFollowerPage();
    }

    /// <summary>
    /// Rebuilds the follower list on the main page if it's open
    /// </summary>
    private static void RefreshMainPage()
    {
        if (_mainPage == null || !_mainPage.IsThisPageOpen) return;

        _followersCategory.ClearChildren();
        _followersCategory.CategoryName = $"Followers ({FollowerController.FollowerControllers.Count})";

        if (FollowerController.FollowerControllers.Count == 0)
        {
            _followersCategory.AddTextBlock("You have no followers spawned. Spawn a NavMeshFollower prop to control it here");
            return;
        }

        foreach (var controller in FollowerController.FollowerControllers)
        {
            var button = _followersCategory.AddContentButton(GetPropName(controller.Spawnable), "", "",
                "Control this follower", "", new List<string> { controller.GetStatus() });
            button.OnPress += () =>
            {
                _selectedFollower = controller;
                _followerPage.OpenPage();
            };
            LoadPropImage(button, controller.Spawnable.guid);
        }
    }

    private static void RefreshFollowerPage()
    {
        if (_followerPage == null || !_followerPage.IsThisPageOpen) return;

        // The follower was deleted while we were looking at it
        if (_selectedFollower == null)
        {
            QuickMenuAPI.GoBack();
            return;
        }

        _followerPage.PageDisplayName = GetPropName(_selectedFollower.Spawnable);
        _followerPage.MenuTitle = GetPropName(_selectedFollower.Spawnable);
        _followerPage.MenuSubtitle = _selectedFollower.GetStatus();
    }

    internal static void RefreshPickupPage()
    {
        if (_pickupPage == null || !_pickupPage.IsThisPageOpen || PlayerSetup.Instance == null) return;

        _propPickupsCategory.ClearChildren();
        _worldPickupsCategory.ClearChildren();

        var playerPos = PlayerSetup.Instance.GetPlayerPosition();

        var propPickups = Pickups.AvailableSpawnablePickups
            .Where(p => p != null && (_selectedFollower == null || p.Spawnable != _selectedFollower.Spawnable))
            .OrderBy(p => Vector3.Distance(p.transform.position, playerPos))
            .ToList();

        _propPickupsCategory.CategoryName = $"Props ({propPickups.Count})";
        foreach (var pickup in propPickups)
        {
            var spawnerName = CVRPlayerManager.Instance.TryGetPlayerName(pickup.Spawnable.ownerId);
            var distance = Vector3.Distance(pickup.transform.position, playerPos);
            var button = _propPickupsCategory.AddContentButton(GetPropName(pickup.Spawnable), "", "",
                "Choose this prop", "", new List<string> { $"Spawned by {spawnerName}", $"{distance:F1}m away" });
            button.OnPress += () => OnPickupPressed(pickup);
            LoadPropImage(button, pickup.Spawnable.guid);
        }

        var worldPickups = Pickups.AvailableObjectSyncPickups
            .Where(p => p != null)
            .OrderBy(p => Vector3.Distance(p.transform.position, playerPos))
            .ToList();

        _worldPickupsCategory.CategoryName = $"World Pickups ({worldPickups.Count})";
        foreach (var pickup in worldPickups)
        {
            var distance = Vector3.Distance(pickup.transform.position, playerPos);
            _worldPickupsCategory.AddButton($"{pickup.objectSync.name} ({distance:F1}m)", "", "Choose this world pickup")
                .OnPress += () => OnPickupPressed(pickup);
        }
    }

    private static void ChoosePickup(Action<Pickups.PickupWrapper> onChosen)
    {
        _onPickupChosen = onChosen;
        _pickupPage.OpenPage();
    }

    private static void OnPickupPressed(Pickups.PickupWrapper pickup)
    {
        // The pickup can be deleted while the list is open
        if (pickup == null)
        {
            QuickMenuAPI.ShowAlertToast("That pickup doesn't exist anymore", 3);
            RefreshPickupPage();
            return;
        }

        if (_selectedFollower == null)
        {
            QuickMenuAPI.ShowAlertToast("That follower doesn't exist anymore", 3);
            QuickMenuAPI.GoBack();
            return;
        }

        _onPickupChosen?.Invoke(pickup);
    }

    private static bool TryGetSelectedFollower(out FollowerController controller)
    {
        controller = _selectedFollower;
        if (controller != null) return true;
        QuickMenuAPI.ShowAlertToast("That follower doesn't exist anymore", 3);
        return false;
    }

    private static void RunIfAllowed(UIPlayerObject player, Action action)
    {
        if (player.IsLocalUser
            || NavMeshFollower.TestMode
            || Friends.FriendsWith(player.Uuid)
            || RequestLibIntegration.HasPermission(player.Uuid))
        {
            action();
            return;
        }

        if (!RequestLibIntegration.HasRequestLib(player.Uuid))
        {
            QuickMenuAPI.ShowNotice("No Permission",
                $"{player.Username} isn't your friend and doesn't have RequestLib, so your followers can't interact with them.");
            return;
        }

        if (RequestLibIntegration.IsRequestPending(player.Uuid))
        {
            QuickMenuAPI.ShowAlertToast($"Still waiting for {player.Username} to reply", 3);
            return;
        }

        QuickMenuAPI.ShowConfirm("Ask for Permission?",
            $"{player.Username} isn't your friend. Ask them if they allow your followers to target them? " +
            $"It lasts until they leave the instance.",
            () => RequestLibIntegration.RequestToInteract(player.Username, player.Uuid, () =>
            {
                action();
                RefreshMenus();
            }),
            yesText: "Ask", noText: "Cancel");
    }

    private static void Follow(FollowerController controller, string playerId)
    {
        if (controller == null) return;
        var followPlayer = controller.GetBehavior<FollowPlayer>();
        followPlayer.DisableAllBehaviorsExcept(followPlayer);
        followPlayer.SetTarget(playerId);
    }

    private static void FetchFor(FollowerController controller, Pickups.PickupWrapper pickup, string playerId)
    {
        if (controller == null) return;
        var fetchPickup = controller.GetBehavior<FetchPickup>();
        fetchPickup.DisableAllBehaviorsExcept(fetchPickup);
        fetchPickup.FetchPickupTo(pickup, playerId);
    }

    private static void PlayFetchWith(FollowerController controller, Pickups.PickupWrapper pickup)
    {
        if (controller == null) return;
        var playFetch = controller.GetBehavior<PlayFetch>();
        playFetch.DisableAllBehaviorsExcept(playFetch);
        playFetch.StartPlayingFetch(pickup);
    }

    private static void Stop(FollowerController controller)
    {
        if (controller == null) return;
        foreach (var behavior in controller.Behaviors)
            behavior.Disable();
    }

    private static string GetPropName(CVRSpawnable spawnable)
    {
        var name = spawnable.PropSoul?.ContentMetadata.AssetName;
        return string.IsNullOrEmpty(name) ? spawnable.guid : name;
    }

    private static async void LoadPropImage(ContentButton button, string propGuid)
    {
        button.ButtonIconLoading = true;
        try
        {
            var response = await PedestalInfoBatchProcessor.QueuePedestalInfoRequest(PedestalType.Prop, propGuid);
            if (response != null) button.ButtonIcon = response.ImageUrl;
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"Failed to get the image for the prop {propGuid}: {e.Message}");
        }
        button.ButtonIconLoading = false;
    }
}
