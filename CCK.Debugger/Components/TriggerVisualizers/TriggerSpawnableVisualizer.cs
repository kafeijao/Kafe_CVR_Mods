using ABI.CCK.Components;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.TriggerVisualizers;

public class TriggerSpawnableVisualizer : TriggerVisualizer {

    private CVRSpawnableTrigger _trigger;

    private const float _fadeDuration = .35f;

    private bool triggered;
    private float _durationInverse;
    private float _timer;
    private Color _triggerColor;

    protected override void Start() {
        base.Start();

        _trigger = (CVRSpawnableTrigger) TriggerBehavior;

        VisualizerGo.transform.localScale = Vector3.zero;

        Events.Spawnable.SpawnableTriggerTriggered += trigger => {
            if (_trigger.enterTasks.Contains(trigger)) {
                _durationInverse = 1f / _fadeDuration;
                _timer = 0;
                _triggerColor = Color.green;
                triggered = true;
            }
            if (_trigger.exitTasks.Contains(trigger)) {
                _durationInverse = 1f / _fadeDuration;
                _timer = 0;
                _triggerColor = Color.red;
                triggered = true;
            }
        };

        Events.Spawnable.SpawnableStayTriggerTriggered += trigger => {
            // Lets let the fades play instead of replacing all the time with this trigger
            if (triggered) return;

            if (_trigger.stayTasks.Contains(trigger)) {
                _durationInverse = 1f / _fadeDuration;
                _timer = 0;
                _triggerColor = Color.yellow;
                triggered = true;
            }
        };
    }

    private void Update() {
        // Update the size and position to match the trigger
        VisualizerGo.transform.localScale = TriggerCollider.size;
        VisualizerGo.transform.localPosition = TriggerCollider.center;

        // Pop in and then fade effect
        if (!triggered) return;
        var effectPercentage = _timer * _durationInverse;
        if (effectPercentage > 1f) {
            triggered = false;
        }
        MaterialStandard.SetColor(Misc.MatMainColor, Color.Lerp(_triggerColor, Misc.ColorWhiteFade, effectPercentage));
        MaterialNeitri.SetFloat(Misc.MatOutlineWidth, Mathf.Lerp(1f, 0.8f, effectPercentage));
        MaterialNeitri.SetColor(Misc.MatOutlineColor, Color.Lerp(_triggerColor, Misc.ColorWhite, effectPercentage));
        _timer += Time.deltaTime;
    }
}
