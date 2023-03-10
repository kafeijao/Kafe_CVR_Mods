using ABI_RC.Core.IO;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.Camera;
using ABI_RC.Systems.Camera.VisualMods;
using ABI.CCK.Components;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class MarioCameraMod : ICameraVisualMod, ICameraVisualModRequireUpdate {

    public static MarioCameraMod Instance;

    private readonly List<CVRSM64Mario> _ourMarios = new();
    private CVRSM64Mario _currentMario;
    private bool _wasNullAlready;

    private float _distance;
    private float _elevation;


    private Sprite _image;
    private Transform _camera;
    private PortableCamera _portableCamera;
    private Transform _cameraParent;
    private bool _isEnabled;

    private SphereCollider _collider;
    private Rigidbody _rigidbody;
    private CVRPickupObject _pickup;

    // First person
    private Vector3 _velocity = Vector3.zero;


    public MarioCameraMod() {
        Instance = this;
    }

    public void UpdateOurMarios(IEnumerable<CVRSM64Mario> ourMarios) {
        _ourMarios.Clear();
        _ourMarios.AddRange(ourMarios);
    }

    public bool IsControllingMario(CVRSM64Mario mario) => _isEnabled && _portableCamera.IsActive() && mario == _currentMario;
    public Transform GetCameraTransform() => _camera.transform;

    private readonly List<Renderer> _renderersToTurnOn = new();

    // Overrides
    public string GetModName(string language) => "Mario 64";
    public Sprite GetModImage() => _image;
    public int GetSortingOrder() => 10;
    public bool ActiveIsOrange() => false;
    public bool DefaultIsOn() => false;

    private bool ChangeMario() {
        if (_currentMario == null) {
            _currentMario = _ourMarios.FirstOrDefault();
        }
        else if (_ourMarios.Count > 0) {
            var currentIndex = _ourMarios.IndexOf(_currentMario);
            _currentMario = _ourMarios[(currentIndex + 1) % _ourMarios.Count];
        }
        return _currentMario != null;
    }

    public void Setup(PortableCamera camera, Camera cameraComponent) {

        _image = CVRSuperMario64.GetMarioSprite();

        _portableCamera = camera;
        _camera = cameraComponent.transform;
        _cameraParent = _camera.transform.parent;
        _collider = _camera.gameObject.AddComponent<SphereCollider>();

        _pickup = _camera.gameObject.GetComponent<CVRPickupObject>();
        _pickup.SetWasUsingGravity(false);

        _rigidbody = _camera.gameObject.GetComponent<Rigidbody>();

        camera.@interface.AddAndGetHeader(this, typeof(MarioCameraMod));

        // Hide mario renderers from the camera
        Camera.onPreCull += cam => {
            if (cam == cameraComponent && _isEnabled && _portableCamera.IsActive() && _currentMario != null) {
                _renderersToTurnOn.Clear();
                foreach (var renderer in _currentMario.GetRenderersToHideFromCamera()) {
                    if (!renderer.enabled) continue;
                    renderer.enabled = false;
                    _renderersToTurnOn.Add(renderer);
                }
            }
        };
        Camera.onPostRender += cam => {
            if (cam == cameraComponent && _renderersToTurnOn.Count > 0) {
                foreach (var renderer in _renderersToTurnOn) {
                    renderer.enabled = true;
                }
                _renderersToTurnOn.Clear();
            }
        };

        var nextMarioButton = camera.@interface.AddAndGetSetting(PortableCameraSettingType.Bool);
        nextMarioButton.BoolChanged = boolChanged => ChangeMario();
        nextMarioButton.SettingName = "MarioNext";
        nextMarioButton.DisplayName = "Mario Next";
        nextMarioButton.isExpertSetting = false;
        nextMarioButton.OriginType = typeof(MarioCameraMod);
        nextMarioButton.DefaultValue = false;
        nextMarioButton.Load();

        var radiusSetting = camera.@interface.AddAndGetSetting(PortableCameraSettingType.Float);
        radiusSetting.FloatChanged = f => _distance = f;
        radiusSetting.SettingName = "MarioCamDistance";
        radiusSetting.DisplayName = "Mario Camera Distance";
        radiusSetting.isExpertSetting = false;
        radiusSetting.OriginType = typeof(MarioCameraMod);
        radiusSetting.DefaultValue = 1f;
        radiusSetting.MinValue = 0f;
        radiusSetting.MaxValue = 2f;
        radiusSetting.Load();

        var elevationSetting = camera.@interface.AddAndGetSetting(PortableCameraSettingType.Float);
        elevationSetting.FloatChanged = f => _elevation = f;
        elevationSetting.SettingName = "MarioCamElevation";
        elevationSetting.DisplayName = "Mario Camera Elevation";
        elevationSetting.isExpertSetting = false;
        elevationSetting.OriginType = typeof(MarioCameraMod);
        elevationSetting.DefaultValue = 0.40f;
        elevationSetting.MinValue = 0f;
        elevationSetting.MaxValue = 2f;
        elevationSetting.Load();

        Disable();
    }

    public void Update() {
        if (!_isEnabled) return;

        // Disable if there is no more
        if (_currentMario == null) {
            if (_wasNullAlready) return;
            if (!ChangeMario()) {
                // Disable the camera mod since we got no marios
                CohtmlHud.Instance.ViewDropText("CVRSM64Camera exited since we are not controlling any marios.", "");
                SchedulerSystem.AddJob(DisableDelayed, 0.05f, 0.0f, 1);
                _wasNullAlready = true;
                return;
            }
        }
        _wasNullAlready = false;

        if (Input.GetKeyDown(KeyCode.E)) _portableCamera.MakePhoto();

        if (_currentMario.IsFirstPerson()) {

            var multiplier = _currentMario.IsSwimming() ? 3 : 2;

            // Calculate the camera position based on the target position and the camera height and distance
            var targetPosition = _currentMario.transform.position + Vector3.up * _elevation / multiplier - _currentMario.transform.forward * _distance / multiplier;
            // Damp the camera movement to make it smooth
            _camera.transform.position = Vector3.SmoothDamp(_camera.transform.position, targetPosition, ref _velocity, 0.3f);
            // Look at the target
            _camera.transform.LookAt(_currentMario.transform);
        }
        else {
            var m = _currentMario.transform.position;
            var n = _camera.position;
            m.y = 0;
            n.y = 0;
            n = (n - m).normalized * _distance;
            n = Quaternion.AngleAxis( MarioInputModule.Instance.horizontal, Vector3.up ) * n;
            n += m;
            n.y = _currentMario.transform.position.y + _elevation;
            _camera.transform.position = n;
            _camera.transform.LookAt( _currentMario.transform.position );
        }




        _portableCamera.RefreshFadeOut();
    }

    public void Enable() {
        if (PlayerSetup.Instance.CheckInSeat()) {
            CohtmlHud.Instance.ViewDropText("Unable to enter CVRSM64Camera while in seat", "");
            SchedulerSystem.AddJob(DisableDelayed, 0.05f, 0.0f, 1);
        }

        else {

            // Check if mario is null, and if it is attempt to get one
            if (_currentMario == null && !ChangeMario()) {
                CohtmlHud.Instance.ViewDropText("Unable to enter CVRSM64Camera while not controlling any mario", "");
                SchedulerSystem.AddJob(DisableDelayed, 0.05f, 0.0f, 1);
                return;
            }

            _isEnabled = true;

            _camera.transform.SetParent(null, true);

            _collider.isTrigger = true;
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _pickup.enabled = false;
            _portableCamera.DisableModByType(typeof(DroneMode));
            _portableCamera.DisableModByType(typeof(AutoOrbit));
            _portableCamera.DisableModByType(typeof(ImageStabilization));

            _portableCamera.SetFlip(CameraFlip.Back);
        }
    }

    public void DisableDelayed() => _portableCamera.DisableModByType(typeof(MarioCameraMod));

    public void Disable() {
        _isEnabled = false;

        _pickup.enabled = true;

        _camera.transform.SetParent(_cameraParent, false);
        _camera.transform.localPosition = Vector3.zero;
        _portableCamera.RestoreFlip();
    }
}
