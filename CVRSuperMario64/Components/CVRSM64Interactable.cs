using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64Interactable : MonoBehaviour {

    private enum InteractableType {
        VanishCap,
        MetalCap,
        WingCap,
    }

    [SerializeField] private InteractableType interactableType = InteractableType.MetalCap;

    private static readonly List<CVRSM64Interactable> InteractableObjects = new();

    public static void HandleInteractables(CVRSM64Mario mario, uint currentStateFlags) {

        // Trigger Caps if is close enough to the proper interactable (if it's already triggered will be ignored)
        foreach (var interactable in InteractableObjects) {
            if (Vector3.Distance(interactable.transform.position, mario.transform.position) > 0.1) continue;
            if (interactable.interactableType == InteractableType.VanishCap) {
                mario.WearCap(currentStateFlags, Utils.MarioCapType.VanishCap, true);
            }
            if (interactable.interactableType == InteractableType.MetalCap) {
                mario.WearCap(currentStateFlags, Utils.MarioCapType.MetalCap, true);
            }
            if (interactable.interactableType == InteractableType.WingCap) {
                mario.WearCap(currentStateFlags, Utils.MarioCapType.WingCap, true);
            }
        }
    }

    private void OnEnable() {
        if (InteractableObjects.Contains(this)) return;
        InteractableObjects.Add(this);
        #if DEBUG
        MelonLogger.Msg($"[{nameof(CVRSM64Interactable)}] {gameObject.name} Enabled! Type: {interactableType.ToString()}");
        #endif
    }

    private void OnDisable() {
        if (!InteractableObjects.Contains(this)) return;
        InteractableObjects.Remove(this);
        #if DEBUG
        MelonLogger.Msg($"[{nameof(CVRSM64Interactable)}] {gameObject.name} Disabled!");
        #endif
    }

    private void OnDestroy() {
        OnDisable();
    }
}
