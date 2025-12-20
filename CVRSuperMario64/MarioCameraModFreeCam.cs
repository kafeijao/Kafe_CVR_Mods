using ABI_RC.Systems.Camera;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class MarioCameraModFreeCam : ICameraVisualMod {

    private static MarioCameraModFreeCam _instance;

    private Sprite _image;
    private bool _isEnabled;

    public MarioCameraModFreeCam() {
        _instance = this;
    }

    public static bool IsFreeCamEnabled() {
        return _instance._isEnabled;
    }

    // Overrides
    public string GetModName(string language) => "Mario Free Cam";
    public Sprite GetModImage() => _image;
    public int GetSortingOrder() => 10;
    public bool ActiveIsOrange() => true;
    public bool DefaultIsOn() => false;

    public void Setup(PortableCamera camera, Camera cameraComponent) {
        _image = CVRSuperMario64.GetMarioArrowsSprite();
        camera.@interface.AddAndGetHeader(this, nameof(MarioCameraModFreeCam));
        Disable();
    }

    public void Enable() {
        _isEnabled = true;
    }

    public void Disable() {
        _isEnabled = false;
    }

    public PortableCamera.CaptureMode GetSupportedCaptureModes()
    {
        return PortableCamera.CaptureMode.Picture;
    }
}
