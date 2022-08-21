using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using CCK.Debugger.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Components.MenuHandlers;

public static class AvatarMenuHandler {

    static AvatarMenuHandler() {
        Events.DebuggerMenu.ControlsNextPage += () => {
            _pageIncrement = 1;
        };
        Events.DebuggerMenu.ControlsPreviousPage += () => {
            _pageIncrement = - 1;
        };
        
        PlayerEntities = new ();
        
        SyncedParametersValues = new();
        LocalParametersValues = new();
        CoreParametersValues = new();
        
        CoreParameterNames = Traverse.Create(typeof(CVRAnimatorManager)).Field("coreParameters").GetValue<HashSet<string>>();
    }
    
    private static readonly List<CVRPlayerEntity> PlayerEntities;
    
    // Current Spawnable Prop Data
    private const int LOCAL_PLAYER_INDEX = -1;
    private static int _currentPlayerIndex = LOCAL_PLAYER_INDEX;
    private static CVRPlayerEntity _currentPlayer;
    
    private static int _pageIncrement;
    private static bool _playerChanged;
    
    private static void UpdateCurrentPlayer() {
        
        // Update indexes
        PlayerEntities.Clear();
        for (var index = 0; index < CVRPlayerManager.Instance.NetworkPlayers.Count; index++) {
            var playerEntity = CVRPlayerManager.Instance.NetworkPlayers[index];
            
            // Ignore players if they're null
            if (playerEntity?.PuppetMaster == null) continue;
            var animatorManager = Traverse.Create(playerEntity.PuppetMaster).Field("_animatorManager").GetValue<CVRAnimatorManager>();
            if (animatorManager == null) continue;
            
            PlayerEntities.Add(playerEntity);
        }
        
        _currentPlayerIndex = PlayerEntities.IndexOf(_currentPlayer);
        
        // If there is no match, reset the player, this will be us
        if (_currentPlayerIndex == LOCAL_PLAYER_INDEX && (
                _currentPlayer != null || 
                _mainAnimator == null || 
                !_mainAnimator.isInitialized)) {
            _playerChanged = true;
            _currentPlayer = null;
            return;
        }
        
        // Otherwise update the index with the page increment count
        // And temporarily represent the local player index as being the Count value
        // And pretend the count is 1 higher for the local player calcs
        if (_pageIncrement != 0) {
            var fakeCount = PlayerEntities.Count + 1;
            if (_currentPlayerIndex == LOCAL_PLAYER_INDEX) _currentPlayerIndex = fakeCount - 1;
            
            _currentPlayerIndex = (_currentPlayerIndex + fakeCount + _pageIncrement) % fakeCount;
            _pageIncrement = 0;
            
            // Let's pick the max value to be us
            if (_currentPlayerIndex == fakeCount - 1) {
                _currentPlayerIndex = LOCAL_PLAYER_INDEX;
                _currentPlayer = null;
            }
            // Otherwise pick the normal chosen
            else {
                _currentPlayer = PlayerEntities[_currentPlayerIndex];
            }
            _playerChanged = true;
        }
    }

    // Attributes
    private static TextMeshProUGUI _attributeUsername;
    private static TextMeshProUGUI _attributeAvatar;
    
    private static Animator _mainAnimator;
    
    // Animator Synced Parameters
    private static GameObject _categorySyncedParameters;
    private static readonly Dictionary<AnimatorControllerParameter, TextMeshProUGUI> SyncedParametersValues;

    // Animator Local Parameters
    private static GameObject _categoryLocalParameters;
    private static readonly Dictionary<AnimatorControllerParameter, TextMeshProUGUI> LocalParametersValues;
    
    // Core Parameters
    private static readonly HashSet<string> CoreParameterNames;
    private static GameObject _coreParameters;
    private static readonly Dictionary<AnimatorControllerParameter, TextMeshProUGUI> CoreParametersValues;
    
    public static void Init(Menu menu) {
        menu.AddNewDebugger("Avatars");
        
        var categoryAttributes = menu.AddCategory("Attributes");
        _attributeUsername = menu.AddCategoryEntry(categoryAttributes, "User Name:");
        _attributeAvatar = menu.AddCategoryEntry(categoryAttributes, "Avatar Name/ID:");
        
        _categorySyncedParameters = menu.AddCategory("Avatar Synced Parameters");
        _categoryLocalParameters = menu.AddCategory("Avatar Local Parameters");
        _coreParameters = menu.AddCategory("Avatar Default Parameters");
        
        menu.ToggleCategories(true);

        _playerChanged = true;
    }

    public static void Update(Menu menu) {
        
        UpdateCurrentPlayer();
        
        var playerCount = PlayerEntities.Count + 1;
        
        menu.SetControlsExtra($"({_currentPlayerIndex+2}/{playerCount})");
        
        menu.ShowControls(playerCount > 1);
        
        var playerUserName = _currentPlayerIndex == LOCAL_PLAYER_INDEX ? MetaPort.Instance.username : _currentPlayer.Username;
        var playerAvatarName = menu.GetAvatarName(_currentPlayerIndex == LOCAL_PLAYER_INDEX ? MetaPort.Instance.currentAvatarGuid : _currentPlayer.AvatarId);
        
        // Avatar Data Info
        _attributeUsername.SetText(playerUserName);
        _attributeAvatar.SetText(playerAvatarName);

        // Update the menus if the spawnable changed
        if (_playerChanged) {
            
            _mainAnimator = _currentPlayerIndex == LOCAL_PLAYER_INDEX 
                ? Events.Avatar.LocalPlayerAnimatorManager?.animator 
                : Traverse.Create(_currentPlayer.PuppetMaster).Field("_animatorManager").GetValue<CVRAnimatorManager>().animator;

            if (_mainAnimator == null || !_mainAnimator.isInitialized || _mainAnimator.parameters == null || _mainAnimator.parameters.Length < 1) return;
            
            // Highlight on local player makes us lag for some reason
            if (_currentPlayerIndex != LOCAL_PLAYER_INDEX) {
                Highlighter.SetTargetHighlight(_mainAnimator.gameObject);
            }
            else {
                Highlighter.ClearTargetHighlight();
            }

            // Restore parameters
            menu.ClearCategory(_categorySyncedParameters);
            menu.ClearCategory(_categoryLocalParameters);
            menu.ClearCategory(_coreParameters);
            SyncedParametersValues.Clear();
            LocalParametersValues.Clear();
            CoreParametersValues.Clear();
            foreach (var parameter in _mainAnimator.parameters) {
                if (parameter.name.StartsWith("#")) {
                    var tmpParamValue = menu.AddCategoryEntry(_categoryLocalParameters, parameter.name);
                    LocalParametersValues[parameter] = tmpParamValue;
                }
                else if (CoreParameterNames.Contains(parameter.name)) {
                    var tmpParamValue = menu.AddCategoryEntry(_coreParameters, parameter.name);
                    CoreParametersValues[parameter] = tmpParamValue;
                }
                else {
                    var tmpParamValue = menu.AddCategoryEntry(_categorySyncedParameters, parameter.name);
                    SyncedParametersValues[parameter] = tmpParamValue;
                }
            }
            
            // Consume the spawnable changed
            _playerChanged = false;
        }
        
        // Update main animator parameter values
        if (_mainAnimator != null && _mainAnimator.isInitialized) {
            foreach (var syncedParametersValue in SyncedParametersValues) {
                var value = Utils.Misc.ParseParameter(syncedParametersValue.Key, _mainAnimator.GetFloat(syncedParametersValue.Key.nameHash));
                syncedParametersValue.Value.SetText(value);
            }
            foreach (var localParametersValue in LocalParametersValues) {
                var value = Utils.Misc.ParseParameter(localParametersValue.Key, _mainAnimator.GetFloat(localParametersValue.Key.nameHash));
                localParametersValue.Value.SetText(value);
            }
            foreach (var coreParametersValue in CoreParametersValues) {
                var value = Utils.Misc.ParseParameter(coreParametersValue.Key, _mainAnimator.GetFloat(coreParametersValue.Key.nameHash));
                coreParametersValue.Value.SetText(value);
            }
        }
    }
}