using ABI_RC.Core.Player;
using ABI_RC.Systems.Movement;
using ABI.CCK.Components;
using ECM2;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.MovementParentFlight;

public class MovementParentFlight : MelonMod
{
    /// <summary>
    /// How far the held movement parent can move from where it started before it flies the player, in metres
    /// </summary>
    private const float DeadZone = 0.02f;

    /// <summary>
    /// How far the held movement parent can twist from where it started before it turns the player, in degrees
    /// </summary>
    private const float TurnDeadZone = 3f;

    /// <summary>
    /// The held movement parent we are standing on, or null when we are not on one
    /// </summary>
    private static Transform _heldParent;

    /// <summary>
    /// The point on the held movement parent that was under our feet when we stepped on it, in its own space
    /// </summary>
    private static Vector3 _heldParentFeet;

    /// <summary>
    /// The held movement parent's rotation when we stepped on it, relative to the player's rotation
    /// </summary>
    private static Quaternion _heldParentStartRotation;

    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();
    }

    /// <summary>
    /// Flies the player while they stand on a movement parent they hold
    /// </summary>
    private static bool FlyOnHeldParent(BetterBetterCharacterController controller)
    {
        CharacterMovement movement = controller.characterMovement;
        Collider ground = movement.groundCollider;
        if (!ModConfig.MeEnabled.Value
            || controller.IsSitting()
            || !movement.isGrounded
            || !ground
            || !IsMovementParent(ground)
            || !PlayerSetup.Instance.IsHolding(ground))
        {
            _heldParent = null;
            return false;
        }

        // Some pickups have no rigidbody, so follow the pickup itself
        Transform parent = ground.GetComponentInParent<CVRPickupObject>().transform;
        Quaternion facing = movement.rotation;
        Quaternion parentRotation = Quaternion.Inverse(facing) * parent.rotation;
        Vector3 standing = PlayerSetup.Instance.GetPlayerPosition();
        if (parent != _heldParent)
        {
            _heldParent = parent;
            _heldParentFeet = parent.InverseTransformPoint(standing);
            _heldParentStartRotation = parentRotation;
            return true;
        }

        // How far the spot we stepped on moved away from our feet
        Vector3 push = parent.TransformPoint(_heldParentFeet) - standing;
        float pace = PastDeadZone(push.magnitude, DeadZone, ModConfig.MeSpeedPerMetre.Value, ModConfig.MeMaxSpeed.Value);

        Vector3 twisted = parentRotation * Quaternion.Inverse(_heldParentStartRotation) * Vector3.forward;
        float twist = Mathf.Atan2(twisted.x, twisted.z) * Mathf.Rad2Deg;
        float turn = PastDeadZone(Mathf.Abs(twist), TurnDeadZone, ModConfig.MeTurnPerDegree.Value, ModConfig.MeMaxTurn.Value);
        if (turn > 0f) controller.TurnBy(Quaternion.AngleAxis(Mathf.Sign(twist) * turn * Time.deltaTime, facing * Vector3.up));

        // Turning swings us around the playspace center
        Vector3 offset = standing - PlayerSetup.Instance.GetPlayerPosition() + push.normalized * (pace * Time.deltaTime);
        if (offset.sqrMagnitude >= 0.00000001f) controller.OffsetBy(offset);
        return true;
    }

    private static bool IsMovementParent(Collider collider)
        => BetterBetterCharacterController._allCollidersAreMovementParents
           || (collider.TryGetComponent(out CVRMovementParent movementParent) && movementParent.isActiveAndEnabled);

    /// <summary>
    /// Turns how far something went past its dead zone into a rate, capped at max
    /// </summary>
    private static float PastDeadZone(float amount, float deadZone, float rate, float max)
        => Mathf.Min(Mathf.Max(amount - deadZone, 0f) * rate, max);

    [HarmonyPatch]
    internal class HarmonyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BetterBetterCharacterController), nameof(BetterBetterCharacterController.FollowRidden))]
        public static bool Before_BetterBetterCharacterController_FollowRidden(BetterBetterCharacterController __instance)
        {
            try
            {
                return !FlyOnHeldParent(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(Before_BetterBetterCharacterController_FollowRidden)}", e);
                return true;
            }
        }
    }
}
