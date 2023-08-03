using System.Collections;
using ABI_RC.Systems.Camera;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using ZXing.Unity;

namespace Kafe.QRCode;

public class QRCodeCameraMod : ICameraVisualModRequireUpdate {

    private Camera _cam;
    private IBarcodeReader _reader;

    private volatile int _camPixelHeight;
    private volatile int _camPixelWidth;
    private volatile bool _captureInProgress = false;

    private void Start() {
        _cam = PortableCamera.Instance._camera;
        _camPixelHeight = _cam.pixelHeight;
        _camPixelWidth = _cam.pixelWidth;
        _reader = new BarcodeReader();
    }

    public void Update() {
        if (_captureInProgress) return;
        _captureInProgress = true;
        MelonCoroutines.Start(CaptureScreen());
    }

    private IEnumerator CaptureScreen() {
        // Wait for the end of the current frame to ensure that all rendering by the GPU is complete
        yield return new WaitForEndOfFrame();

        // Request a readback of the GPU data
        AsyncGPUReadback.Request(_cam.activeTexture, 0, TextureFormat.RGBA32, OnCompleteReadback);
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request) {
        try {
            if (request.hasError) {
                MelonLogger.Msg("Failed to read GPU data");
                _captureInProgress = false;
                return;
            }
            var colors = request.GetData<Color32>().ToArray();
            Task.Run(() => DecodeColor32(colors));
        }
        catch (Exception e) {
            MelonLogger.Error("Error while trying to decode QRCodes from a AsyncGPUReadbackRequest.");
            MelonLogger.Error(e);
            _captureInProgress = false;
        }
    }

    // Keep track of the last time each barcode was printed
    private readonly Dictionary<string, DateTime> _lastPrintTime = new();

    private void DecodeColor32(Color32[] colors) {
        try {
            var results = _reader.DecodeMultiple(colors, _camPixelWidth, _camPixelHeight);
            if (results == null) {
                // MelonLogger.Msg("There was no barcodes found :(");
                return;
            }
            // MelonLogger.Msg($"Found {results.Length} Barcodes!");
            foreach (var result in results) {
                if (result == null) continue;


                // Get the current time
                var now = DateTime.UtcNow;

                // If the barcode has not been printed in the last 20 seconds
                if (!_lastPrintTime.ContainsKey(result.Text) || now - _lastPrintTime[result.Text] > TimeSpan.FromSeconds(20)) {
                    MelonLogger.Msg($"Found QR Code: {result.Text}");

                    // Update the last print time
                    _lastPrintTime[result.Text] = now;
                }
            }
        }
        finally {
            _captureInProgress = false;
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PortableCamera), nameof(PortableCamera.Start))]
        public static void After_PortableCamera_Start(PortableCamera __instance) {
            var camMod = new QRCodeCameraMod();
            camMod.Start();
            __instance.RequireUpdate(camMod);
        }
    }
}
