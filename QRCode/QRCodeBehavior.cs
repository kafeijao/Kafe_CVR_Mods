using System.Collections;
using System.Collections.Concurrent;
using ABI_RC.Systems.Camera;
using Kafe.QRCode.ResultHandlers;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using ZXing.Unity;

namespace Kafe.QRCode;

public class QRCodeBehavior : MonoBehaviour {

    // Queue with barcode results, this will be populated by async tasks
    internal static readonly ConcurrentQueue<ResultHandler.Result> BarcodeParsedResults = new();

    // Keep track of the last time each barcode was printed
    private readonly ConcurrentDictionary<string, DateTime> _lastPrintTime = new();

    private Camera _cam;
    private IBarcodeReader _reader;

    private volatile int _camPixelHeight;
    private volatile int _camPixelWidth;
    private volatile bool _captureInProgress;

    // Unity UI
    private Transform _contentTrx;
    private const string ContentPath = "Scroll View/Viewport/Content";
    private Transform _resultTemplateTrx;
    private const string ResultTemplatePath = "Templates/Template_QRResult";
    private const string ResultImagePath = "Image";
    private const string ResultTitlePath = "Body/Title";
    private const string ResultMessagePath = "Body/Message";

    private void Start() {
        _cam = PortableCamera.Instance._camera;
        _reader = new BarcodeReader();

        name = $"[{nameof(QRCode)} Mod]";
        gameObject.layer = LayerMask.NameToLayer("UI Internal");

        var trx = transform;
        trx.localRotation = Quaternion.identity;
        trx.localPosition = new Vector3(100f, -9.25f, 0);
        trx.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        // Find Unity UI Components
        _contentTrx = trx.Find(ContentPath);
        _resultTemplateTrx = trx.Find(ResultTemplatePath);
    }

    private void OnEnable() {
        if (_contentTrx == null) return;
        foreach (Transform child in _contentTrx) {
            Destroy(child.gameObject);
        }
        _lastPrintTime.Clear();
    }

    private void Update() {

        // Handle barcode results if present
        while (BarcodeParsedResults.TryDequeue(out var result)) {
            HandleBarcodeResult(result);
        }

        // Check if we should capture
        if (_captureInProgress) return;
        _captureInProgress = true;
        MelonCoroutines.Start(DelayedCaptureScreen());
    }

    private void HandleBarcodeResult(ResultHandler.Result result) {

        var qrCodeEntryTrx = Instantiate(_resultTemplateTrx, _contentTrx, false);
        qrCodeEntryTrx.SetAsFirstSibling();

        var button = qrCodeEntryTrx.GetComponent<Button>();
        var img = qrCodeEntryTrx.Find(ResultImagePath).GetComponent<Image>();
        var title = qrCodeEntryTrx.Find(ResultTitlePath).GetComponent<TextMeshProUGUI>();
        var message = qrCodeEntryTrx.Find(ResultMessagePath).GetComponent<TextMeshProUGUI>();

        title.text = result.Type;
        message.text = result.Message;
        img.sprite = result.Sprite;
        button.onClick.AddListener(() => result.Handler());

        // Setup the self-destruction
        StartCoroutine(DelayedRemoveBarcode(qrCodeEntryTrx.gameObject, result));
    }

    private IEnumerator DelayedRemoveBarcode(GameObject barcodeGo, ResultHandler.Result result) {
        // Delete barcodes after being displayed for X amount
        yield return new WaitForSeconds(20f);
        if (barcodeGo != null) Destroy(barcodeGo);
        _lastPrintTime.TryRemove(result.Message, out _);
    }

    private IEnumerator DelayedCaptureScreen() {
        // Wait for X seconds
        yield return new WaitForSeconds(Math.Clamp(ModConfig.MeQRScanIntervalSeconds.Value, 1, 240));

        // Now start capturing screen
        yield return CaptureScreen();
    }

    private IEnumerator CaptureScreen() {
        // Wait for the end of the current frame to ensure that all rendering by the GPU is complete
        yield return new WaitForEndOfFrame();

        // Request a readback of the GPU data
        _camPixelHeight = _cam.pixelHeight;
        _camPixelWidth = _cam.pixelWidth;
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

    private void DecodeColor32(Color32[] colors) {
        try {
            var results = _reader.DecodeMultiple(colors, _camPixelWidth, _camPixelHeight);
            if (results == null) {
                return;
            }

            foreach (var result in results) {
                if (result == null || string.IsNullOrWhiteSpace(result.Text)) continue;

                // Get the current time
                var now = DateTime.UtcNow;

                var hasLastTime = _lastPrintTime.TryGetValue(result.Text, out var lastTime);

                // If the barcode has not been printed in the last 20 seconds
                if (!hasLastTime || now - lastTime > TimeSpan.FromSeconds(20)) {

                    MelonLogger.Msg($"Scanned Barcode with the content: {result.Text}");

                    // Update the last print time
                    _lastPrintTime.TryAdd(result.Text, now);

                    ResultHandler.ProcessText(result.Text);
                }
            }
        }
        finally {
            _captureInProgress = false;
        }
    }
}
