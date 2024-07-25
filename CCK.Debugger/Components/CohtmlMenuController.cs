using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using cohtml.InputSystem;
using Kafe.CCK.Debugger.Components.CohtmlMenuHandlers;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components;

public class CohtmlMenuController : MonoBehaviour {

    internal static bool Initialized { private set; get; }

    // Internal
    private const string CouiUrl = "coui://UIResources/CCKDebugger";
    internal static CohtmlMenuController Instance;

    public bool Enabled { get; set; }

    // MenuController References
    private Animator _animator;
    public CVRPickupObject Pickup { get; private set; }

    private CohtmlControlledView _cohtmlControlledView;
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

    internal static void Create(CVR_MenuManager quickMenu) {

        // Create game object with the quad to render our cohtml view
        var cohtmlGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cohtmlGo.name = "[CCK.Debugger] Cohtml Menu";

        // Create and initialize
        var cohtmlMenuController = cohtmlGo.AddComponent<CohtmlMenuController>();
        cohtmlMenuController.InitializeMenu(quickMenu);

        // Add handlers
        ICohtmlHandler.Handlers.Add(new AvatarCohtmlHandler());
        ICohtmlHandler.Handlers.Add(new SpawnableCohtmlHandler());
        ICohtmlHandler.Handlers.Add(new WorldCohtmlHandler());
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
                menuControllerTransform.localPosition = new Vector3(-0.8f, 0, 0);
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
        if (Events.DebuggerMenuCohtml.GetLatestCoreToConsume(out var core)) Instance._cohtmlControlledView.View.TriggerEvent("CCKDebuggerCoreUpdate", JsonConvert.SerializeObject(core));
        if (Events.DebuggerMenuCohtml.GetLatestCoreInfoToConsume(out var coreInfo)) Instance._cohtmlControlledView.View.TriggerEvent("CCKDebuggerCoreInfoUpdate",JsonConvert.SerializeObject(coreInfo));
        if (Events.DebuggerMenuCohtml.GetLatestButtonUpdatesToConsume(out var buttons)) Instance._cohtmlControlledView.View.TriggerEvent("CCKDebuggerButtonsUpdate",JsonConvert.SerializeObject(buttons));
        if (Events.DebuggerMenuCohtml.GetLatestSectionUpdatesToConsume(out var sections)) Instance._cohtmlControlledView.View.TriggerEvent("CCKDebuggerSectionsUpdate",JsonConvert.SerializeObject(sections));
    }

    private void Update() {

        // Prevent updates until we swap entity
        if (ICohtmlHandler.Crashed || !Initialized || !_cohtmlControlledView.enabled || !Enabled) return;

        // Crash wrapper to avoid giga lag
        try {

            // Update values via the current handler
            ICohtmlHandler.CurrentHandler?.Update();

            // Send the and consume the cached updates saved in this frame
            ConsumeCachedUpdates();
        }
        catch (Exception e) {
            MelonLogger.Error(e);
            MelonLogger.Error($"Something BORKED really bad, and to prevent lagging we're going to stop the debugger menu updates until click reset.");
            MelonLogger.Error($"Feel free to ping kafeijao#8342 in the #bug-reports channel of ChilloutVR Modding Group discord with the error message.");

            // Add the error to the menu
            ICohtmlHandler.Crash();
        }
    }

    private void InitializeMenu(CVR_MenuManager quickMenu) {

        _quickMenuGo = quickMenu.gameObject;

        SetupListeners();

        if (Instance != null) {
            const string errMsg = "Attempted to start a second instance of the Cohtml menu...";
            MelonLogger.Error(errMsg);
            throw new Exception(errMsg);
        }

        Instance = this;

        SetupControlledView(quickMenu);
    }

    private void SetupListeners() {

        // Handle menu opening/closing
        CVRGameEventSystem.QuickMenu.OnOpen.AddListener(UpdateMenuState);
        CVRGameEventSystem.QuickMenu.OnClose.AddListener(UpdateMenuState);
        CVRGameEventSystem.MainMenu.OnOpen.AddListener(UpdateMenuState);
        CVRGameEventSystem.MainMenu.OnClose.AddListener(UpdateMenuState);

        // Handle the quick menu reloads and reload CCK Debugger with it
        Events.DebuggerMenuCohtml.CohtmlMenuReloaded += FullReload;
    }

    private void FullReload() {
        if (!Initialized) return;

        MelonLogger.Msg("Reloading Cohtml menu...");

        Initialized = false;

        // If the menu is disabled, lets enable it so it can process the reload
        if (!_cohtmlControlledView.enabled) {
            _cohtmlControlledView.enabled = true;
        }

        _cohtmlControlledView.View.Reload();

        // Reload current handler
        ICohtmlHandler.Reload();

        // Mark as initialized and update state
        Initialized = true;
        UpdateMenuState();
    }

    internal void UpdateMenuState() {

        if (!Initialized) return;

        // Menu should not be running if set to the quick menu and the menu is not opened
        var isMenuEnabled = !ModConfig.MeIsHidden.Value
                            // If attached to the quick menu, it needs to be opened
                            && (_currentMenuParent != MenuTarget.QuickMenu || CVR_MenuManager.Instance.IsQuickMenuOpen)
                            // In desktop the if the big menu is opened, we need to close our menu
                            && (MetaPort.Instance.isUsingVr || !ViewManager.Instance.IsMainMenuOpen);

        if (!isMenuEnabled) {
            // Clear the highlight when the menu is not enabled
            Utils.Highlighter.ClearTargetHighlight();
        }

        enabled = isMenuEnabled;
        _cohtmlControlledView.enabled = isMenuEnabled;
        _cohtmlControlledView.Enabled = isMenuEnabled;
        _animator.SetBool(AnimatorIdOpen, isMenuEnabled);
    }

    private void SetupControlledView(CVR_MenuManager quickMenu) {

        var quickMenuGo = quickMenu.gameObject;

        // Copy the quick menu layer, last time I checked was UI Internal
        gameObject.layer = quickMenuGo.layer;

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
        _animator.runtimeAnimatorController = quickMenu.quickMenuAnimator.runtimeAnimatorController;

        // Setup pickup script
        Pickup = gameObject.AddComponent<CVRPickupObject>();
        Pickup.enabled = false;

        // Save collider
        _cohtmlViewCollider = gameObject.GetComponent<Collider>();

        // Create and set up the Cohtml view controller
        _cohtmlControlledView = gameObject.AddComponent<CohtmlControlledView>();

        CohtmlViewInputHandler.RegisterView(_cohtmlControlledView);

        CohtmlInputHandler.Input.OnKeyEvent += OnKeyEvent;
        CohtmlInputHandler.Input.OnCharEvent += OnCharEvent;
        CohtmlInputHandler.Input.OnTouchEvent += OnTouchEvent;

        _cohtmlControlledView.Listener.ReadyForBindings += RegisterMenuViewEvents;
        // _cohtmlControlledView.Listener.FinishLoad += OnFinishedLoad;

        // Calculate the resolution
        var resolutionX = (int)(_scaleX * 2500);
        var resolutionY = (int)(_scaleY * 2500);

        _cohtmlControlledView.CohtmlUISystem = quickMenu.quickMenu.CohtmlUISystem;
        _cohtmlControlledView.AudioSource = quickMenu.quickMenu.AudioSource;
        // _cohtmlView.AutoFocus = false;
        _cohtmlControlledView.IsTransparent = true;
        _cohtmlControlledView.PixelPerfect = true;
        _cohtmlControlledView.Width = resolutionX;
        _cohtmlControlledView.Height = resolutionY;
        _cohtmlControlledView.Page = CouiUrl + "/index.html";

        _cohtmlControlledView.enabled = true;
    }

    public void OnKeyEvent(KeyEvent eventData, InputEventWrapper unityEvent) {
        if (!CVR_MenuManager.Instance._quickMenuReady || !Initialized || !enabled) return;
        eventData.Send(_cohtmlControlledView);
    }

    public void OnCharEvent(KeyEvent eventData, char character) {
        if (!CVR_MenuManager.Instance._quickMenuReady || !Initialized || !enabled) return;
        eventData.Send(_cohtmlControlledView);
    }

    public void OnTouchEvent(TouchEventCollection touches) {
        if (!CVR_MenuManager.Instance._quickMenuReady || !Initialized || !enabled) return;
        for (uint key = 0; key < touches.Capacity; ++key) {
            if (touches[key].IsActive)
                touches[key].Send(_cohtmlControlledView);
        }
    }

    private void RegisterMenuViewEvents() {

        var view = _cohtmlControlledView.View;
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
            MelonLogger.Msg("Cohtml menu has loaded successfully!");

            // Todo: Move next to _cohtmlControlledView.Listener.ReadyForBindings when we get a better callback
            _cohtmlControlledView.Listener.FinishLoad += OnFinishedLoad;
        });
    }

    private void OnFinishedLoad(string url) {
        _cohtmlControlledView.Listener.FinishLoad -= OnFinishedLoad;

        _cohtmlControlledView.View.TriggerEvent("CCKDebuggerModReady");

        // Reload current handler
        ICohtmlHandler.Reload();

        // Mark as initialized and update state
        Initialized = true;
        UpdateMenuState();
    }
}
