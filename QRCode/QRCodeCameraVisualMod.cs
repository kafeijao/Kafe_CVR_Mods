using ABI_RC.Systems.Camera;
using MelonLoader;
using UnityEngine;

namespace Kafe.QRCode;

public class QRCodeCameraVisualMod : ICameraVisualMod {

    private QRCodeBehavior _qrCodeBehavior;

    public string GetModName(string language) => "QRCode Reader";

    public Sprite GetModImage() => ModConfig.ImageSprites[ModConfig.ImageType.QRCode];

    public int GetSortingOrder() => 10;

    public bool ActiveIsOrange() => true;

    public bool DefaultIsOn() => false;

    public void Setup(PortableCamera camera, Camera cameraComponent) {
        try {
            var targetTransform = camera.cameraCanvasGroup.transform.parent;
            var qrMenu = UnityEngine.Object.Instantiate(ModConfig.QRCodePrefab, targetTransform, false);
            _qrCodeBehavior = qrMenu.AddComponent<QRCodeBehavior>();

            camera.@interface.AddAndGetHeader(this, nameof(QRCodeCameraVisualMod));
            Disable();
        }
        catch (Exception e) {
            MelonLogger.Error($"Error during {nameof(QRCodeCameraVisualMod)}.{nameof(Setup)}");
            MelonLogger.Error(e);
        }
    }

    public void Enable() {
        _qrCodeBehavior.gameObject.SetActive(true);
    }

    public void Disable() {
        _qrCodeBehavior.gameObject.SetActive(false);
    }

    public PortableCamera.CaptureMode GetSupportedCaptureModes()
    {
        return PortableCamera.CaptureMode.Picture;
    }
}
