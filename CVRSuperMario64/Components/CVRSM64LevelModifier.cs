using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64LevelModifier : MonoBehaviour {

    public enum ModifierType {
        Water,
        Gas,
    }

    [SerializeField] private List<Animator> animators = new();
    [SerializeField] private ModifierType modifierType = ModifierType.Water;


    [NonSerialized] private static float _lastLevel = float.MinValue;
    [NonSerialized] private static ModifierType _lastModifierType = ModifierType.Water;
    [NonSerialized] private static readonly List<CVRSM64LevelModifier> LevelModifierObjects = new();

    [NonSerialized] private static bool _forceUpdate = false;

    private enum LocalParameterNames {
        IsActive,
        HasMod,
    }

    private static readonly Dictionary<LocalParameterNames, int> LocalParameters = new() {
        { LocalParameterNames.IsActive, Animator.StringToHash(nameof(LocalParameterNames.IsActive)) },
        { LocalParameterNames.HasMod, Animator.StringToHash(nameof(LocalParameterNames.HasMod)) },
    };

    private void Start() {

        // Check the animators
        var toNuke = new HashSet<Animator>();
        foreach (var animator in animators) {
            if (animator == null || animator.runtimeAnimatorController == null) {
                toNuke.Add(animator);
            }
            else {
                animator.SetBool(LocalParameters[LocalParameterNames.HasMod], true);
            }
        }
        foreach (var animatorToNuke in toNuke) animators.Remove(animatorToNuke);
        if (toNuke.Count > 0) {
            var animatorsToNukeStr = toNuke.Select(animToNuke => animToNuke.gameObject.name);
            MelonLogger.Warning($"[{nameof(CVRSM64LevelModifier)}] Removing animators: {string.Join(", ", animatorsToNukeStr)} because they were null or had no controllers slotted.");
        }
    }

    public static void MarkForUpdate() {
        _forceUpdate = true;
    }

    public static void ContextTick(List<CVRSM64Mario> marios) {

        if (LevelModifierObjects.Count == 0) {
            if (Mathf.Approximately(_lastLevel, float.MinValue)) return;
            lock (marios) {
                foreach (var mario in marios) {
                    Interop.SetLevelModifier(mario.marioId, _lastModifierType, float.MinValue);
                }
            }
            _lastLevel = float.MinValue;
            return;
        }

        // Get the highest level
        var maxLevelModifier = LevelModifierObjects[0];
        foreach (var levelModifier in LevelModifierObjects) {
            if (levelModifier.transform.position.y > maxLevelModifier.transform.position.y) {
                maxLevelModifier = levelModifier;
            }
        }
        var highestLevel = maxLevelModifier.transform.position.y;

        // Highest level hasn't changed, lets ignore (unless we're forcing the update)
        if ((maxLevelModifier.modifierType == _lastModifierType && Mathf.Approximately(highestLevel, _lastLevel)) || _forceUpdate) {
            return;
        }

        _lastModifierType = maxLevelModifier.modifierType;
        _lastLevel = highestLevel;
        _forceUpdate = false;

        lock (marios) {
            foreach (var mario in marios) {
                Interop.SetLevelModifier(mario.marioId, _lastModifierType, highestLevel);
            }
        }

        // Update animators
        foreach (var levelModifier in LevelModifierObjects) {
            foreach (var animator in levelModifier.animators) {
                animator.SetBool(LocalParameters[LocalParameterNames.IsActive], levelModifier == maxLevelModifier);
            }
        }
    }

    private void OnEnable() {
        if (LevelModifierObjects.Contains(this)) return;
        LevelModifierObjects.Add(this);
        #if DEBUG
        MelonLogger.Msg($"[{nameof(CVRSM64LevelModifier)}] {gameObject.name} Enabled! Type: {modifierType.ToString()}");
        #endif
    }

    private void OnDisable() {
        if (!LevelModifierObjects.Contains(this)) return;
        LevelModifierObjects.Remove(this);
        #if DEBUG
        MelonLogger.Msg($"[{nameof(CVRSM64LevelModifier)}] {gameObject.name} Disabled!");
        #endif
    }
}
