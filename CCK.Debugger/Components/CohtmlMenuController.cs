using System.Collections;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using CCK.Debugger.Components.CohtmlMenuHandlers;
using cohtml;
using cohtml.Net;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace CCK.Debugger.Components;

public class CohtmlMenuController : MonoBehaviour {

    internal static bool Initialized { private set; get; }

    // Internal
    private const string CouiUrl = "coui://UIResources/CCKDebugger";
    internal static CohtmlMenuController Instance;

    private static bool _errored;

    // MenuController References
    private Animator _animator;
    private CVRPickupObject _pickup;
    public CVRPickupObject Pickup => _pickup;

    private CohtmlView _cohtmlView;
    private Collider _cohtmlViewCollider;

    // MenuController Parent References
    private GameObject _quickMenuGo;

    // Menu Current Settings
    private MenuTarget _currentMenuParent;
    internal MenuTarget CurrentMenuParent => _currentMenuParent;

    private float _scaleX, _scaleY;

    // Hashed IDs
    private static readonly int AnimatorIdOpen = Animator.StringToHash("Open");
    private static readonly int ShaderIdDissolvePattern = Shader.PropertyToID("_DesolvePattern");
    private static readonly int ShaderIdDissolveTiming = Shader.PropertyToID("_DesolveTiming");

    internal static void Create(GameObject targetGo) {

        // Create game object with the quad to render our cohtml view
        var cohtmlGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cohtmlGo.name = "[CCK.Debugger] Cohtml Menu";

        // Create and initialize
        var cohtmlMenuController = cohtmlGo.AddComponent<CohtmlMenuController>();
        cohtmlMenuController.InitializeMenu(targetGo);

        // Add handlers
        ICohtmlHandler.Handlers.Add(new AvatarCohtmlHandler());
        ICohtmlHandler.Handlers.Add(new SpawnableCohtmlHandler());
        ICohtmlHandler.Handlers.Add(new MiscCohtmlHandler());

        Events.DebuggerMenu.MainNextPage += () => ICohtmlHandler.SwitchMenu(true);
        Events.DebuggerMenu.MainPreviousPage += () => ICohtmlHandler.SwitchMenu(false);
    }

    internal enum MenuTarget {
        QuickMenu,
        World,
        HUD,
    }

    internal void ParentTo(MenuTarget targetType) {

        var menuControllerTransform = transform;

        switch (targetType) {
            case MenuTarget.QuickMenu:
                menuControllerTransform.SetParent(_quickMenuGo.transform, true);
                menuControllerTransform.localPosition = new Vector3(-0.7f, 0, 0);
                menuControllerTransform.localRotation = Quaternion.identity;
                menuControllerTransform.localScale = new Vector3(_scaleX, _scaleY, 1f);
                break;

            case MenuTarget.World:
                var pos = menuControllerTransform.position;
                var rot = menuControllerTransform.rotation;
                menuControllerTransform.SetParent(null, true);
                menuControllerTransform.SetPositionAndRotation(pos, rot);
                break;

            case MenuTarget.HUD:
                var target = MetaPort.Instance.isUsingVr
                    ? PlayerSetup.Instance.vrCamera
                    : PlayerSetup.Instance.desktopCamera;
                menuControllerTransform.SetParent(target.transform, true);
                break;
        }

        _currentMenuParent = targetType;
        UpdateMenuState();
    }

    internal static void ConsumeCachedUpdates() {
        if (Events.DebuggerMenuCohtml.GetLatestCoreToConsume(out var core)) Instance._cohtmlView.View.TriggerEvent("CCKDebuggerCoreUpdate", JsonConvert.SerializeObject(core));
        if (Events.DebuggerMenuCohtml.GetLatestCoreInfoToConsume(out var coreInfo)) Instance._cohtmlView.View.TriggerEvent("CCKDebuggerCoreInfoUpdate",JsonConvert.SerializeObject(coreInfo));
        if (Events.DebuggerMenuCohtml.GetLatestButtonUpdatesToConsume(out var buttons)) Instance._cohtmlView.View.TriggerEvent("CCKDebuggerButtonsUpdate",JsonConvert.SerializeObject(buttons));
        if (Events.DebuggerMenuCohtml.GetLatestSectionUpdatesToConsume(out var sections)) Instance._cohtmlView.View.TriggerEvent("CCKDebuggerSectionsUpdate",JsonConvert.SerializeObject(sections));
    }

    private void Update() {

        // Prevent updates until we swap entity
        if (ICohtmlHandler.Crashed || !Initialized || !_cohtmlView.enabled) return;

        // Crash wrapper to avoid giga lag
        try {

            // Update values via the current handler
            ICohtmlHandler.CurrentHandler?.Update();

            // Send the and consume the cached updates saved in this frame
            ConsumeCachedUpdates();
        }
        catch (Exception e) {
            MelonLogger.Error(e);
            MelonLogger.Error($"Something BORKED really bad, and to prevent lagging we're going to stop the debugger menu updates until you swap entity.");
            MelonLogger.Error($"Feel free to ping kafeijao#8342 in the #bug-reports channel of ChilloutVR Modding Group discord with the error message.");

            // Add the error to the menu
            Core.OnCrash();

            ICohtmlHandler.Crashed = true;
        }
    }

    private void InitializeMenu(GameObject targetGo) {

        _quickMenuGo = targetGo;

        SetupListeners();

        if (Instance != null) {
            const string errMsg = "Attempted to start a second instance of the Cohtml menu...";
            MelonLogger.Error(errMsg);
            throw new Exception(errMsg);
        }

        Instance = this;

        // Start the initializing code when all other components are initialized
        StartCoroutine(DelayedInitialize(targetGo));
    }

    private void SetupListeners() {

        // Handle menu opening/closing
        Events.QuickMenu.QuickMenuIsShownChanged += _ => UpdateMenuState();

        // Handle the quick menu reloads and reload CCK Debugger with it
        Events.DebuggerMenuCohtml.CohtmlMenuReloaded += FullReload;
    }

    private void FullReload() {
        if (!Initialized) return;

        MelonLogger.Msg("Reloading Cohtml menu...");

        Initialized = false;

        // If the menu is disabled, lets enable it so it can process the reload
        if (!_cohtmlView.enabled) {
            _cohtmlView.enabled = true;
        }

        _cohtmlView.View.Reload();
    }

    private void UpdateMenuState() {
        _errored = false;

        if (!Initialized) return;

        // Menu should not be running if set to the quick menu and the menu is not opened
        var isMenuEnabled = _currentMenuParent != MenuTarget.QuickMenu || Events.QuickMenu.IsQuickMenuOpened;

        enabled = isMenuEnabled;
        _cohtmlView.enabled = isMenuEnabled;
        _animator.SetBool(AnimatorIdOpen, isMenuEnabled);
    }

    private void RegisterMenuViewEvents() {

        var view = _cohtmlView.View;
        var menuManager = CVR_MenuManager.Instance;

        // Update cohtml material
        var material = gameObject.GetComponent<MeshRenderer>().materials[0];
        material.SetTexture(ShaderIdDissolvePattern, menuManager.pattern);
        material.SetTexture(ShaderIdDissolveTiming, menuManager.timing);
        material.SetTextureScale(ShaderIdDissolvePattern, new Vector2(1f, 1f));

        view.BindCall("CVRAppCallSystemCall", new Action<string, string, string, string, string>(menuManager.HandleSystemCall));

        // Button Click
        view.BindCall("CCKDebuggerButtonClick", new Action<string>(typeStr => {
            if (!Enum.TryParse<Button.ButtonType>(typeStr, out var buttonType)) {
                MelonLogger.Error($"Tried to parse a non-existing type of button: {typeStr}");
                return;
            }
            Core.ClickButton(buttonType);
        }));

        // Menu navigation controls
        view.RegisterForEvent("CCKDebuggerMenuNext", Events.DebuggerMenu.OnMainNextPage);
        view.RegisterForEvent("CCKDebuggerMenuPrevious", Events.DebuggerMenu.OnMainPrevious);
        view.RegisterForEvent("CCKDebuggerControlsNext", Events.DebuggerMenu.OnControlsNext);
        view.RegisterForEvent("CCKDebuggerControlsPrevious", Events.DebuggerMenu.OnControlsPrevious);

        view.RegisterForEvent("CCKDebuggerMenuReady", () => {
            MelonLogger.Msg($"Cohtml menu has loaded successfully!");

            view.TriggerEvent("CCKDebuggerModReady");

            // Reload current handler
            ICohtmlHandler.Reload();

            // Mark as initialized and update state
            Initialized = true;
            UpdateMenuState();
        });
    }

    private IEnumerator DelayedInitialize(GameObject targetGo) {

        // Wait for the Cohtml animator and UI systems to be initialized
        GameObject cwv;
        CohtmlUISystem cohtmlUISystem;
        while ((cwv = GameObject.Find("/Cohtml/CohtmlWorldView")) == null) {
            yield return null;
        }
        var cohtmlWorldViewAnimator = cwv.GetComponent<Animator>().runtimeAnimatorController;
        while ((cohtmlUISystem = GameObject.Find("/Cohtml/CohtmlUISystem").GetComponent<CohtmlUISystem>()) == null) {
            yield return null;
        }

        try {

            // Copy the quick menu layer, last time I checked was UI Internal
            gameObject.layer = targetGo.layer;

            // Set the dimensions of the quad (menu size)
            _scaleX = 0.5f;
            _scaleY = 0.6f;

            // Parent and position our game object
            ParentTo(MenuTarget.QuickMenu);

            // Setup mesh renderer
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            meshRenderer.sortingLayerID = 0;
            meshRenderer.sortingOrder = 10;

            // Setup the animator
            _animator = gameObject.AddComponent<Animator>();
            _animator.runtimeAnimatorController = cohtmlWorldViewAnimator;

            // Setup pickup script
            _pickup = gameObject.AddComponent<CVRPickupObject>();
            _pickup.enabled = false;

            // Save collider
            _cohtmlViewCollider = gameObject.GetComponent<Collider>();

            // Create and set up the Cohtml view
            _cohtmlView = gameObject.AddComponent<CohtmlView>();
            _cohtmlView.Listener.ReadyForBindings += RegisterMenuViewEvents;

            // Calculate the resolution
            var resolutionX = (int)(_scaleX * 2500);
            var resolutionY = (int)(_scaleY * 2500);

            _cohtmlView.CohtmlUISystem = cohtmlUISystem;
            _cohtmlView.AutoFocus = false;
            _cohtmlView.IsTransparent = true;
            _cohtmlView.PixelPerfect = true;
            _cohtmlView.Width = resolutionX;
            _cohtmlView.Height = resolutionY;
            _cohtmlView.Page = CouiUrl + "/index.html";

            _cohtmlView.enabled = true;

            MelonLogger.Msg($"Initialized Cohtml CCK.Debugger with a resolution of: {resolutionX} x {resolutionY}");
        }
        catch (Exception e) {
            MelonLogger.Error("Error while executing CohtmlMenu Initialize ");
            MelonLogger.Error(e);
            throw;
        }
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        private static bool _vrMInteractDownOnMenu;
        private static Vector2 _vrQuickMenuLastCoords;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ControllerRay), "LateUpdate")]
        private static void After_ControllerRay_LateUpdate(ControllerRay __instance) {
            if (_errored) return;
            try {
                LateUpdateCCKRayController(__instance);
            }
            catch (Exception e) {
                _errored = true;
                MelonLogger.Error(e);
                MelonLogger.Error("Execution of the menu will stop. Report this error to the creator.");
                throw;
            }
        }

        private static bool RaycastCohtmlPlane(CohtmlView view, Ray ray, out float distance, out Vector2 viewCoords) {
            if (new Plane(view.transform.forward, view.transform.position).Raycast(ray, out distance)) {
                Vector3 vector3 = view.transform.InverseTransformPoint(ray.origin + ray.direction * distance);
                viewCoords = new Vector2(vector3.x + 0.5f, vector3.y + 0.5f);
                return viewCoords.x >= 0.0 && viewCoords.x <= 1.0 && viewCoords.y >= 0.0 && viewCoords.y <= 1.0;
            }
            distance = -1f;
            viewCoords = Vector2.zero;
            return false;
        }

        private static void LateUpdateCCKRayController(ControllerRay controllerRay) {

            if (!Initialized || !Instance._cohtmlView.enabled) return;

            // Check the ray hands
            var interactingWithCurrentHand = controllerRay.hand
                ? CVRInputManager.Instance.interactLeftDown
                : CVRInputManager.Instance.interactRightDown;
            var stoppedInteractingWithCurrentHand = controllerRay.hand
                ? CVRInputManager.Instance.interactLeftUp
                : CVRInputManager.Instance.interactRightUp;

            var ray = new Ray(controllerRay.transform.position,
                controllerRay.transform.TransformDirection(controllerRay.RayDirection));

            // Check if the raycast intercepts the menu
            if (RaycastCohtmlPlane(Instance._cohtmlView, ray, out var distance, out var viewCoords)) {

                // Mark as pointing the menu
                CVRInputManager.Instance.controllerPointingMenu = true;

                if (!controllerRay.uiActive) return;

                // Get the values for x and y
                var x = (int)(viewCoords.x * Instance._cohtmlView.Width);
                var y = (int)((1.0 - viewCoords.y) * Instance._cohtmlView.Height);

                _vrQuickMenuLastCoords.x = x;
                _vrQuickMenuLastCoords.y = y;

                // Mouse move event
                var mouseMove = new MouseEventData { X = x, Y = y, Type = MouseEventData.EventType.MouseMove };
                Instance._cohtmlView.View.MouseEvent(mouseMove);

                // Mouse down event
                if (interactingWithCurrentHand && !_vrMInteractDownOnMenu) {
                    var mouseDown = new MouseEventData { X = x, Y = y, Type = MouseEventData.EventType.MouseDown };
                    Instance._cohtmlView.View.MouseEvent(mouseDown);
                    _vrMInteractDownOnMenu = true;
                }

                // Mouse up event
                if (stoppedInteractingWithCurrentHand && _vrMInteractDownOnMenu) {
                    var mouseup = new MouseEventData { X = x, Y = y, Type = MouseEventData.EventType.MouseUp };
                    Instance._cohtmlView.View.MouseEvent(mouseup);
                    _vrMInteractDownOnMenu = false;
                }

                // Mouse wheel event
                if (CVRInputManager.Instance.scrollValue > 0.0 || CVRInputManager.Instance.scrollValue < 0.0) {
                    var mouseWheel = new MouseEventData {
                        WheelX = 0.0f, WheelY = CVRInputManager.Instance.scrollValue * -750f,
                        Type = MouseEventData.EventType.MouseWheel
                    };
                    Instance._cohtmlView.View.MouseEvent(mouseWheel);
                }

                // Clear targeted candidates and highlights, because we're selecting our menu. I could've used a
                // transpiler to properly fix this, but Daky hates me if I do
                var controllerRayTraverse = Traverse.Create(controllerRay);
                controllerRayTraverse.Method("clearTelepathicGrabTargetHighlight").GetValue();
                controllerRayTraverse.Field("_telepathicPickupCandidate").SetValue(null);
                controllerRayTraverse.Field("_telepathicPickupTargeted").SetValue(false);

                // If the line renderer doesn't exist, ignore
                if (!controllerRay.lineRenderer) return;

                controllerRay.lineRenderer.enabled = true;
                var rayPos = controllerRay.transform.position + controllerRay.transform.TransformDirection(controllerRay.RayDirection) * distance;
                controllerRay.lineRenderer.SetPosition(1, controllerRay.transform.InverseTransformPoint(rayPos));
            }

            // If it was being hold down, release
            else if (stoppedInteractingWithCurrentHand && _vrMInteractDownOnMenu) {
                Instance._cohtmlView.View.MouseEvent(new MouseEventData {
                    X = (int) _vrQuickMenuLastCoords.x,
                    Y = (int) _vrQuickMenuLastCoords.y,
                    Type = MouseEventData.EventType.MouseUp,
                });
                _vrMInteractDownOnMenu = false;
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), "LateUpdate")]
        private static void After_CVR_MenuManager_LateUpdate(CVR_MenuManager __instance) {
            if (_errored) return;
            try {
                LateUpdateMenuManager(__instance);
            }
            catch (Exception e) {
                _errored = true;
                MelonLogger.Error(e);
                MelonLogger.Error("Execution of the menu will stop. Report this error to the creator.");
                throw;
            }
        }

        private static bool _desktopMouseDownOnMenu;
        private static bool _desktopMouseMovingOnMenu;
        private static Vector2 _desktopQuickMenuLastCoords;

        private static void LateUpdateMenuManager(CVR_MenuManager menuManager) {
            if (!Initialized) return;

            var cckView = Instance._cohtmlView;
            var isCursorLocked = Cursor.lockState == CursorLockMode.Locked;

            var menuManagerTraverse = Traverse.Create(menuManager);
            var camera = menuManagerTraverse.Field("_camera").GetValue<Camera>();
            var desktopMouseMode = menuManagerTraverse.Field("_desktopMouseMode").GetValue<bool>();

            if (!cckView.enabled || !desktopMouseMode || MetaPort.Instance.isUsingVr || camera == null) return;

            int x = -1, y = -1;
            bool hasHit;

            // Calculate menu raycast using the camera as a raycast source point (when the menu is in world space)
            if (isCursorLocked) {
                var ray = new Ray(camera.transform.position, camera.transform.TransformDirection(Vector3.forward));
                hasHit = RaycastCohtmlPlane(cckView, ray, out var hitInfoCamera, out var viewCoords);
                if (hasHit) {
                    x = (int)(viewCoords.x * cckView.Width);
                    y = (int)((1.0 - viewCoords.y) * cckView.Height);
                }
            }
            // Calculate menu raycast using the cursor as the raycast source point (when menu is on quick menu or hud)
            else {
                var ray = camera.ScreenPointToRay(Input.mousePosition);
                hasHit = Instance._cohtmlViewCollider.Raycast(ray, out var hitInfoCursor, 1000f);
                if (hasHit) {
                    x = (int) (hitInfoCursor.textureCoord.x * cckView.Width);
                    y = (int) ((1.0 - hitInfoCursor.textureCoord.y) * cckView.Height);
                }
            }

            if (hasHit)  {
                // Mark as pointing the menu
                CVRInputManager.Instance.controllerPointingMenu = true;

                // Grab the x and y positions
                _desktopQuickMenuLastCoords.x = x;
                _desktopQuickMenuLastCoords.y = y;

                // Mouse move event
                cckView.View.MouseEvent(new MouseEventData { X = x, Y = y, Type = MouseEventData.EventType.MouseMove });
                _desktopMouseMovingOnMenu = true;

                // Mouse down event
                if (Input.GetMouseButtonDown(0)) {
                    cckView.View.MouseEvent(new MouseEventData { X = x, Y = y, Type = MouseEventData.EventType.MouseDown });
                    _desktopMouseDownOnMenu = true;
                }

                // Mouse up event
                if (Input.GetMouseButtonUp(0)) {
                    cckView.View.MouseEvent(new MouseEventData { X = x, Y = y, Type = MouseEventData.EventType.MouseUp });
                    _desktopMouseDownOnMenu = false;
                }

                // Mouse scroll event
                if (CVRInputManager.Instance.scrollValue > 0.0 || CVRInputManager.Instance.scrollValue < 0.0) {
                    cckView.View.MouseEvent(new MouseEventData { WheelX = 0.0f, WheelY = CVRInputManager.Instance.scrollValue * -750f, Type = MouseEventData.EventType.MouseWheel });
                }
            }

            // Detect the mouse got out of the menu
            if (_desktopMouseMovingOnMenu) {
                cckView.View.MouseEvent(new MouseEventData { X = -1, Y = -1, Type = MouseEventData.EventType.MouseMove });
                _desktopMouseMovingOnMenu = false;
            }

            // Mouse up event when stopped clicking
            if (Input.GetMouseButtonUp(0) && _desktopMouseDownOnMenu) {
                cckView.View.MouseEvent(new MouseEventData { X = (int) _desktopQuickMenuLastCoords.x, Y = (int) _desktopQuickMenuLastCoords.y, Type = MouseEventData.EventType.MouseUp });
                _desktopMouseDownOnMenu = false;
            }
        }
    }
}
