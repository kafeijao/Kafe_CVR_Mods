using ABI_RC.Core.Player;
using ABI_RC.Core.Player.AvatarTracking.Remote;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using HarmonyLib;
using LibSM64;
using MelonLoader;
using UnityEngine;

namespace CVRSuperMario64;

[DefaultExecutionOrder(999999)]
[RequireComponent(typeof(SM64Mario), typeof(CVRSpawnable))]
public class CVRSpawnableInputProvider : SM64InputProvider {

    [SerializeField]
    private CVRSpawnable Spawnable;
    private CVRPlayerEntity Owner;
    private Traverse<RemoteHeadPoint> OwnerViewPoint;

    // Inputs
    private int _inputHorizontalIndex;
    private CVRSpawnableValue _inputHorizontal;
    private int _inputVerticalIndex;
    private CVRSpawnableValue _inputVertical;
    private int _inputJumpIndex;
    private CVRSpawnableValue _inputJump;
    private int _inputKickIndex;
    private CVRSpawnableValue _inputKick;
    private int _inputStompIndex;
    private CVRSpawnableValue _inputStomp;

    private void LoadInput(out CVRSpawnableValue parameter, out int index, string inputName) {
        try {
            index = Spawnable.syncValues.FindIndex(value => value.name == inputName);
            parameter = Spawnable.syncValues[index];
        }
        catch (ArgumentException) {
            var err = $"{nameof(CVRSpawnableInputProvider)} requires a ${nameof(CVRSpawnable)} with a synced value named ${inputName}!";
            MelonLogger.Error(err);
            Spawnable.Delete();
            throw new Exception(err);
        }
    }

    private void Start() {

        if (!CVRSuperMario64.FilesLoaded) {
            MelonLogger.Error($"The mod files were not properly loaded! Check the errors at the startup!");
            Destroy(this);
            return;
        }

        MelonLogger.Msg($"Initializing a SM64Mario Spawnable...");

        // Check for Spawnable component
        Spawnable = GetComponent<CVRSpawnable>();
        if (Spawnable == null) {
            var err = $"{nameof(CVRSpawnableInputProvider)} requires a ${nameof(CVRSpawnable)} on the same GameObject!";
            MelonLogger.Error(err);
            Destroy(this);
            return;
        }

        if (!Spawnable.IsMine()) {
            Owner = MetaPort.Instance.PlayerManager.NetworkPlayers.Find(entity => entity.Uuid == Spawnable.ownerId);
            OwnerViewPoint = Traverse.Create(Owner.PuppetMaster).Field<RemoteHeadPoint>("_viewPoint");
            if (OwnerViewPoint == null || OwnerViewPoint.Value == null) {
                var err = $"{nameof(CVRSpawnableInputProvider)} failed to start because couldn't find the viewpoint of the owner of it!";
                MelonLogger.Error(err);
                Spawnable.Delete();
                return;
            }
        }

        // Load the spawnable inputs
        LoadInput(out _inputHorizontal, out _inputHorizontalIndex, "Horizontal");
        LoadInput(out _inputVertical, out _inputVerticalIndex, "Vertical");
        LoadInput(out _inputJump, out _inputJumpIndex, "Jump");
        LoadInput(out _inputKick, out _inputKickIndex, "Kick");
        LoadInput(out _inputStomp, out _inputStompIndex, "Stomp");

        // Check for the SM64Mario component
        var mario = GetComponent<SM64Mario>();
        if (mario == null) {
            MelonLogger.Msg($"Adding the ${nameof(SM64Mario)} Component...");
            gameObject.AddComponent<SM64Mario>();
        }

        MelonLogger.Msg($"A SM64Mario Spawnable was initialize! Is ours: {Spawnable.IsMine()}");


        if (Spawnable != null && Spawnable.IsMine()) {
            MarioInputModule.Instance.controllingMarios++;
        }
    }

    // private bool CanWeControl() {
    //     // Check if it's ours
    //     // Other people are syncing it (grabbing/telegrabbing/attatched) sync will be diff than 0
    //     return Spawnable.IsMine() && Spawnable.SyncType == 0;
    // }

    #if DEBUG
    private void Update() {
        if (Input.GetKeyDown(KeyCode.End)) {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.5f);
            foreach (Collider collider in hitColliders) {
                if (!Misc.IsGoodCollider(collider)) continue;
                MelonLogger.Msg("Collider within 0.5 units: " + collider.gameObject.name);
            }
        }
    }

    #endif

    private void OnDestroy() {
        if (Spawnable != null && Spawnable.IsMine()) {
            MarioInputModule.Instance.controllingMarios--;
        }
    }

    public override Vector3 GetCameraLookDirection() {

        // Use our own camera
        if (Spawnable.IsMine()) {
            return PlayerSetup.Instance.GetActiveCamera().transform.forward;
        }

        // Use the remote player viewpoint
        if (OwnerViewPoint.Value) {
            return OwnerViewPoint.Value.transform.forward;
        }

        return Vector3.zero;
    }

    public override Vector2 GetJoystickAxes() {

        // Update the spawnable sync values and send the values
        if (Spawnable.IsMine()) {
            var horizontal = MarioInputModule.Instance.horizontal;
            var vertical = MarioInputModule.Instance.vertical;
            Spawnable.SetValue(_inputHorizontalIndex, horizontal);
            Spawnable.SetValue(_inputVerticalIndex, vertical);
            return new Vector2( horizontal, vertical );
        }

        // Send the current values from the spawnable
        return new Vector2( _inputHorizontal.currentValue, _inputVertical.currentValue );
    }

    public override bool GetButtonHeld( Button button ) {

        if (Spawnable.IsMine()) {

            switch( button ) {
                case Button.Jump: {
                    var jump = MarioInputModule.Instance.jump;
                    Spawnable.SetValue(_inputJumpIndex, jump ? 1f : 0f);
                    return jump;
                }
                case Button.Kick: {
                    var kick = MarioInputModule.Instance.kick;
                    Spawnable.SetValue(_inputKickIndex, kick ? 1f : 0f);
                    return kick;
                }
                case Button.Stomp: {
                    var stomp = MarioInputModule.Instance.stop;
                    Spawnable.SetValue(_inputStompIndex, stomp ? 1f : 0f);
                    return stomp;
                }
            }
            return false;

        }

        switch( button ) {
            case Button.Jump:  return _inputJump.currentValue > 0.5f;
            case Button.Kick:  return _inputKick.currentValue > 0.5f;
            case Button.Stomp: return _inputStomp.currentValue > 0.5f;
        }
        return false;
    }

    public override bool IsMine() => Spawnable.IsMine();
}
