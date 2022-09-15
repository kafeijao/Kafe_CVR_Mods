using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Components.MenuHandlers;

public class AvatarMenuHandler : IMenuHandler {

    static AvatarMenuHandler() {

        // Todo: Allow to inspect other people's avatars if they give permission
        // Todo: Waiting for bios
        var players = CCKDebugger.TestMode ? CVRPlayerManager.Instance.NetworkPlayers : new List<CVRPlayerEntity>();

        bool IsValid(CVRPlayerEntity entity) {
            if (entity?.PuppetMaster == null) return false;
            var animatorManager = Traverse.Create(entity.PuppetMaster)
                .Field("_animatorManager")
                .GetValue<CVRAnimatorManager>();
            return animatorManager != null;
        }

        PlayerEntities = new LooseList<CVRPlayerEntity>(players, IsValid, true);
        CoreParameterNames = Traverse.Create(typeof(CVRAnimatorManager)).Field("coreParameters").GetValue<HashSet<string>>();

        // Update local avatar if we changed avatar and local avatar is selected
        Events.Avatar.AnimatorManagerUpdated += () => {
            if (PlayerEntities.CurrentObject == null) PlayerEntities.HasChanged = true;
        };
    }

    private static readonly LooseList<CVRPlayerEntity> PlayerEntities;

    // Attributes
    private static TextMeshProUGUI _attributeUsername;
    private static TextMeshProUGUI _attributeAvatar;

    private static Animator _mainAnimator;

    // Animator Synced Parameters
    private static GameObject _categorySyncedParameters;

    // Animator Local Parameters
    private static GameObject _categoryLocalParameters;

    // Core Parameters
    private static readonly HashSet<string> CoreParameterNames;
    private static GameObject _coreParameters;

    public void Load(Menu menu) {

        menu.AddNewDebugger("Avatars");

        var categoryAttributes = menu.AddCategory("Attributes");
        _attributeUsername = menu.AddCategoryEntry(categoryAttributes, "User Name:");
        _attributeAvatar = menu.AddCategoryEntry(categoryAttributes, "Avatar Name/ID:");

        _categorySyncedParameters = menu.AddCategory("Avatar Synced Parameters");
        _categoryLocalParameters = menu.AddCategory("Avatar Local Parameters");
        _coreParameters = menu.AddCategory("Avatar Default Parameters");

        menu.ToggleCategories(true);

        PlayerEntities.ListenPageChangeEvents = true;
        PlayerEntities.HasChanged = true;
    }
    public void Unload() {
        PlayerEntities.ListenPageChangeEvents = false;
    }

    public void Update(Menu menu) {

        PlayerEntities.UpdateViaSource();

        var playerCount = PlayerEntities.Count;

        var isLocal = PlayerEntities.CurrentObject == null;
        var currentPlayer = PlayerEntities.CurrentObject;

        menu.SetControlsExtra($"({PlayerEntities.CurrentObjectIndex+1}/{playerCount})");

        menu.ShowControls(playerCount > 1);

        var playerUserName = isLocal ? MetaPort.Instance.username : currentPlayer.Username;
        var playerAvatarName = menu.GetAvatarName(isLocal ? MetaPort.Instance.currentAvatarGuid : currentPlayer.AvatarId);

        // Avatar Data Info
        _attributeUsername.SetText(playerUserName);
        _attributeAvatar.SetText(playerAvatarName);

        // Update the menus if the spawnable changed
        if (PlayerEntities.HasChanged) {

            _mainAnimator = isLocal
                ? Events.Avatar.LocalPlayerAnimatorManager?.animator
                : Traverse.Create(currentPlayer.PuppetMaster).Field("_animatorManager").GetValue<CVRAnimatorManager>().animator;

            if (_mainAnimator == null || !_mainAnimator.isInitialized || _mainAnimator.parameters == null || _mainAnimator.parameters.Length < 1) return;

            // Highlight on local player makes us lag for some reason
            if (isLocal) Highlighter.ClearTargetHighlight();
            else Highlighter.SetTargetHighlight(_mainAnimator.gameObject);

            // Restore parameters
            menu.ClearCategory(_categorySyncedParameters);
            menu.ClearCategory(_categoryLocalParameters);
            menu.ClearCategory(_coreParameters);
            ParameterEntry.Entries.Clear();
            foreach (var parameter in _mainAnimator.parameters) {

                // Generate the text mesh pro for the proper category
                TextMeshProUGUI tmpParamValue;
                if (parameter.name.StartsWith("#")) tmpParamValue = menu.AddCategoryEntry(_categoryLocalParameters, parameter.name);
                else if (CoreParameterNames.Contains(parameter.name)) tmpParamValue = menu.AddCategoryEntry(_coreParameters, parameter.name);
                else tmpParamValue = menu.AddCategoryEntry(_categorySyncedParameters, parameter.name);

                // Create a parameter entry linked to the TextMeshPro
                ParameterEntry.Add(_mainAnimator, parameter, tmpParamValue);
            }

            // Consume the spawnable changed
            PlayerEntities.HasChanged = false;
        }

        // Iterate the parameter entries and update their values
        if (_mainAnimator != null && _mainAnimator.isInitialized) {
            foreach (var entry in ParameterEntry.Entries) entry.Update();
        }
    }
}
