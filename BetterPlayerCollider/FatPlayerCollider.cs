using ABI_RC.Core.Player;
using ABI_RC.Systems.MovementSystem;
using HarmonyLib;
using UnityEngine;

#if DEBUG
using MelonLoader;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
#endif

namespace Kafe.BetterPlayerCollider;

public class FatPlayerCollider : MonoBehaviour {

    private static FatPlayerCollider _instance;
    private static MovementSystem _movementSystem;
    private static CharacterController _characterController;

    private Rigidbody _physics;
    private CapsuleCollider _collider;


    private int _currentFrame;
    private bool _isCollidingWithWall;

    #if DEBUG
    private bool _isCollidingWithWallPrevious = true;
    #endif

    public static bool IsCollidingWithWall() {
        return _instance._isCollidingWithWall;
    }

    public static void Create(MovementSystem movementSystem) {

        var fatColliderGo = new GameObject(nameof(FatPlayerCollider)) {
            layer = 8
        };
        DontDestroyOnLoad(fatColliderGo);

        #if DEBUG
        LabeledVisualizer.Create(fatColliderGo, "FAT Collider");
        #endif

        _movementSystem = movementSystem;
        _characterController = _movementSystem.controller;

        fatColliderGo.AddComponent<FatPlayerCollider>();
    }

    private void Start() {

        _physics = gameObject.AddComponent<Rigidbody>();
        _physics.mass = 50f;
        _physics.useGravity = false;
        _physics.interpolation = RigidbodyInterpolation.Interpolate;

        _physics.isKinematic = true;

        _collider = gameObject.AddComponent<CapsuleCollider>();
        _collider.height = 1.8f;
        _collider.radius = 0.3f;
        _collider.center = Vector3.up * 0.9f;

        Physics.IgnoreCollision(_collider, _movementSystem.controller);
        Physics.IgnoreCollision(_collider, _movementSystem.proxyCollider);
        Physics.IgnoreCollision(_collider, _movementSystem.forceCollider);
        Physics.IgnoreCollision(_collider, _movementSystem.holoPortController);

        _instance = this;
    }

    private void Update() {
        #if DEBUG
        if (_isCollidingWithWallPrevious != _isCollidingWithWall) {
            MelonLogger.Msg($"[FAT COLLIDER] {(_isCollidingWithWall ? "Started" : "Stopped")} Colliding with Wall");
            _isCollidingWithWallPrevious = _isCollidingWithWall;
        }
        #endif
    }

    private void OnControllerColliderHit(ControllerColliderHit hit) {

        // Reset the wall detection
        if (_currentFrame != Time.frameCount) {
            _isCollidingWithWall = false;
            _currentFrame = Time.frameCount;
        }

        // For every collision check if we're colliding with a wall
        if (Vector3.Angle(Vector3.up, hit.normal) > _characterController.slopeLimit) {
            _isCollidingWithWall = true;
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateCollider), typeof(bool))]
        public static void After_MovementSystem_UpdateColliderCenter(MovementSystem __instance, bool updateRadius) {
            if (_instance == null) return;
            if (updateRadius) {
                // Let's make the collider slightly fatter
                _instance._collider.radius = __instance.proxyCollider.radius * 1.1f;
            }
            _instance._collider.height  = __instance.proxyCollider.height;
            _instance._collider.center = __instance.proxyCollider.center;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.FixedUpdate))]
        public static void After_MovementSystem_FixedUpdate(MovementSystem __instance) {
            if (_instance == null || PlayerSetup.Instance._animator == null || PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.Head) == null) return;

            _instance.transform.rotation = Quaternion.identity;
            // _instance.transform.position = PlayerSetup.Instance.GetActiveCamera().transform.position with {
            _instance.transform.position = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.Head).position with {
                y = __instance.transform.position.y
            };
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.SetImmobilized))]
        public static void After_MovementSystem_SetImmobilized(MovementSystem __instance, bool immobilized) {
            if (_instance == null) return;
            _instance.gameObject.SetActive(!immobilized);
        }
    }
}
