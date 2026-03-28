using Kafe.CCK.Debugger.Utils;
using MelonLoader;
using NAK.Contacts;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.TriggerVisualizers;

[DefaultExecutionOrder(999999)]
public class TriggerToContactVisualizer : MonoBehaviour
{
    private static readonly Dictionary<MonoBehaviour, TriggerToContactVisualizer> VisualizersAll = new();
    private static readonly Dictionary<MonoBehaviour, TriggerToContactVisualizer> VisualizersActive = new();

    private const string GameObjectName = "Visualizer";
    private const string GameObjectWrapperName = "[CCK.Debugger] Trigger Visualizer";

    private Material _materialStandard;
    private Material _materialNeitri;

    private GameObject _wrapperGo;
    private TriggerToContact _triggerBehavior;
    private ContactReceiver _receiver;
    private GameObject _visualizerGo;

    private const float FadeDuration = .35f;

    private bool _triggered;
    private float _durationInverse;
    private float _timer;
    private Color _triggerColor;

    public static TriggerToContactVisualizer CreateVisualizer(TriggerToContact trigger)
    {
        // Check if the component already exists, if so ignore the creation request
        var wrapperTransform = trigger.transform.Find(GameObjectWrapperName);
        if (wrapperTransform != null && wrapperTransform.TryGetComponent(out TriggerToContactVisualizer visualizer))
        {
            return visualizer;
        }

        // Create the wrapper
        var wrapper = wrapperTransform == null
            ? new GameObject(GameObjectWrapperName) { layer = trigger.gameObject.layer }
            : wrapperTransform.gameObject;
        wrapper.transform.SetParent(trigger.transform, false);
        wrapper.SetActive(false);

        // Instantiate the proper visualizer for the right type of trigger
        visualizer = wrapper.AddComponent<TriggerToContactVisualizer>();
        visualizer._triggerBehavior = trigger;
        visualizer._receiver = trigger.receiver;
        visualizer.enabled = false;
        visualizer._wrapperGo = wrapper;
        // This wrapper is so we can create the visualizer on a disabled GO to prevent awake from being called before we set the Pointer
        wrapper.SetActive(true);

        return visualizer;
    }

    private void Awake()
    {
        // Needs to be on Awake because OnDestroy is only called if the game object was active, and same goes for awake
        VisualizersAll[_triggerBehavior] = this;
    }

    private void InitializeVisualizer(Mesh mesh)
    {
        _visualizerGo = new GameObject(GameObjectName) { layer = _wrapperGo.layer };

        // Create mesh filter
        var meshFilter = _visualizerGo.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        // Create standard shader material with render type set to Fade
        _materialStandard = new Material(Misc.ShaderStandard);
        _materialStandard.SetInt(Misc.MatSrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _materialStandard.SetInt(Misc.MatDstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _materialStandard.SetInt(Misc.MatZWrite, 0);
        _materialStandard.DisableKeyword(Misc.ShaderAlphaTest);
        _materialStandard.EnableKeyword(Misc.ShaderAlphaBlend);
        _materialStandard.DisableKeyword(Misc.ShaderAlphaPreMultiply);
        _materialStandard.renderQueue = 3000;
        _materialStandard.SetColor(Misc.MatMainColor, Misc.ColorWhiteFade);

        // Create neitri fade outline shader material
        _materialNeitri = new Material(ModConfig.ShaderCache[ModConfig.ShaderType.NeitriDistanceFadeOutline]);
        _materialNeitri.SetFloat(Misc.MatOutlineWidth, 0.8f);
        _materialNeitri.SetFloat(Misc.MatOutlineSmoothness, 0.1f);
        _materialNeitri.SetFloat(Misc.MatFadeInBehindObjectsDistance, 2f);
        _materialNeitri.SetFloat(Misc.MatFadeOutBehindObjectsDistance, 10f);
        _materialNeitri.SetFloat(Misc.MatFadeInCameraDistance, 10f);
        _materialNeitri.SetFloat(Misc.MatFadeOutCameraDistance, 15f);
        _materialNeitri.SetFloat(Misc.MatShowOutlineInFrontOfObjects, 0f);
        _materialNeitri.SetColor(Misc.MatOutlineColor, Misc.ColorWhite);

        // Create the renderer and assign material
        var renderer = _visualizerGo.AddComponent<MeshRenderer>();
        renderer.materials = new[] { _materialStandard, _materialNeitri };

        // Add as a child to the wrapper
        _visualizerGo.transform.SetParent(_wrapperGo.transform, false);

        // Hide by default
        _visualizerGo.SetActive(false);
    }

    private void Start()
    {
        PrimitiveType? primitiveType = _triggerBehavior.receiver.shapeType switch
        {
            ShapeType.Sphere => PrimitiveType.Sphere,
            ShapeType.Capsule => PrimitiveType.Capsule,
            ShapeType.Box => PrimitiveType.Cube,
            _ => null,
        };

        if (primitiveType == null)
        {
            MelonLogger.Error($"Failed to create a trigger visualizer because the shape type is not implemented: " +
                              $"{_triggerBehavior.receiver.shapeType.ToString()}. Contact the Mod Author to fix it");
            Destroy(this);
            return;
        }

        InitializeVisualizer(Misc.GetPrimitiveMesh(primitiveType.Value));

        UpdateState();

        _visualizerGo.transform.localScale = Vector3.zero;

        Events.Avatar.AasTriggerCollided += (trigger, task) =>
        {
            if (trigger != _triggerBehavior) return;
            if (_triggerBehavior.onEnterTasks.Contains(task))
            {
                _durationInverse = 1f / FadeDuration;
                _timer = 0;
                _triggerColor = Color.green;
                _triggered = true;
            }

            if (_triggerBehavior.onExitTasks.Contains(task))
            {
                _durationInverse = 1f / FadeDuration;
                _timer = 0;
                _triggerColor = Color.red;
                _triggered = true;
            }
        };

        // Events.Spawnable.SpawnableStayTriggerTriggered +=
        Events.Avatar.AasStayTriggerExecuted += OnStayTriggerTriggered;
    }

    private void OnStayTriggerTriggered(TriggerToContact trigger, TriggerToContact.ContactTriggerStayTask task)
    {
        if (trigger != _triggerBehavior) return;

        // Lets let the fades play instead of replacing all the time with this trigger
        if (_triggered) return;

        if (_triggerBehavior.onStayTask.Contains(task))
        {
            _durationInverse = 1f / FadeDuration;
            _timer = 0;
            _triggerColor = Color.yellow;
            _triggered = true;
        }
    }

    private void Update()
    {
        var receiver = _receiver;

        // Match transform like ContactBase gizmos
        _visualizerGo.transform.localPosition = receiver.localPosition;
        _visualizerGo.transform.localRotation = receiver.localRotation;

        Vector3 scale = Vector3.one;

        switch (receiver.shapeType)
        {
            case ShapeType.Sphere:
            {
                float diameter = receiver.radius * 2f;
                scale = Vector3.one * diameter;
                break;
            }

            case ShapeType.Capsule:
            {
                float diameter = receiver.radius * 2f;

                // Unity capsule primitive = height 2 with radius 0.5
                scale.x = diameter;
                scale.z = diameter;
                scale.y = receiver.height * 2f;

                break;
            }

            case ShapeType.Box:
            {
                scale = receiver.boxSize;
                break;
            }
        }

        _visualizerGo.transform.localScale = scale;


        // Pop in and then fade effect
        if (!_triggered) return;

        var effectPercentage = _timer * _durationInverse;
        if (effectPercentage > 1f)
        {
            _triggered = false;
        }

        _materialStandard.SetColor(Misc.MatMainColor, Color.Lerp(_triggerColor, Misc.ColorWhiteFade, effectPercentage));
        _materialNeitri.SetFloat(Misc.MatOutlineWidth, Mathf.Lerp(1f, 0.8f, effectPercentage));
        _materialNeitri.SetColor(Misc.MatOutlineColor, Color.Lerp(_triggerColor, Misc.ColorWhite, effectPercentage));
        _timer += Time.deltaTime;
    }

    private void UpdateState()
    {
        if (_visualizerGo == null || _triggerBehavior == null) return;
        _visualizerGo.SetActive(isActiveAndEnabled);
        if (isActiveAndEnabled && !VisualizersActive.ContainsKey(_triggerBehavior))
        {
            VisualizersActive.Add(_triggerBehavior, this);
        }
        else if (!isActiveAndEnabled && VisualizersActive.ContainsKey(_triggerBehavior))
        {
            VisualizersActive.Remove(_triggerBehavior);
        }
    }

    private void OnDestroy()
    {
        if (VisualizersActive.ContainsKey(_triggerBehavior)) VisualizersActive.Remove(_triggerBehavior);
        if (VisualizersAll.ContainsKey(_triggerBehavior)) VisualizersAll.Remove(_triggerBehavior);
    }

    private void OnEnable() => UpdateState();

    private void OnDisable() => UpdateState();

    private void ResetMaterialSettings()
    {
        _materialStandard.SetColor(Misc.MatMainColor, Misc.ColorWhiteFade);
        _materialNeitri.SetFloat(Misc.MatOutlineWidth, 0.8f);
        _materialNeitri.SetColor(Misc.MatOutlineColor, Misc.ColorWhite);
    }

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll()
    {
        // Iterate over a copy of the values because they're going to be removed when disabled
        foreach (var visualizer in VisualizersAll.Values.ToList())
        {
            visualizer.enabled = false;
        }
    }
}
