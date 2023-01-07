using ABI.CCK.Components;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.TriggerVisualizers;

public class TriggerSpawnableVisualizer : TriggerVisualizer {

    private CVRSpawnableTrigger _trigger;

    private const float FadeDuration = .35f;

    private bool _triggered;
    private float _durationInverse;
    private float _timer;
    private Color _triggerColor;

    protected override void Start() {
        base.Start();

        _trigger = (CVRSpawnableTrigger) TriggerBehavior;

        VisualizerGo.transform.localScale = Vector3.zero;

        Events.Spawnable.SpawnableTriggerTriggered += trigger => {
            if (_trigger.enterTasks.Contains(trigger)) {
                _durationInverse = 1f / FadeDuration;
                _timer = 0;
                _triggerColor = Color.green;
                _triggered = true;
            }
            if (_trigger.exitTasks.Contains(trigger)) {
                _durationInverse = 1f / FadeDuration;
                _timer = 0;
                _triggerColor = Color.red;
                _triggered = true;
            }
        };

        Events.Spawnable.SpawnableStayTriggerTriggered += trigger => {
            // Lets let the fades play instead of replacing all the time with this trigger
            if (_triggered) return;

            if (_trigger.stayTasks.Contains(trigger)) {
                _durationInverse = 1f / FadeDuration;
                _timer = 0;
                _triggerColor = Color.yellow;
                _triggered = true;
            }
        };
    }

    private void Update() {
        if (!Initialized) return;

        // Update the size and position to match the trigger
        VisualizerGo.transform.localScale = TriggerCollider.size;
        VisualizerGo.transform.localPosition = TriggerCollider.center;

        // Pop in and then fade effect
        if (!_triggered) return;
        var effectPercentage = _timer * _durationInverse;
        if (effectPercentage > 1f) {
            _triggered = false;
        }
        MaterialStandard.SetColor(Misc.MatMainColor, Color.Lerp(_triggerColor, Misc.ColorWhiteFade, effectPercentage));
        MaterialNeitri.SetFloat(Misc.MatOutlineWidth, Mathf.Lerp(1f, 0.8f, effectPercentage));
        MaterialNeitri.SetColor(Misc.MatOutlineColor, Color.Lerp(_triggerColor, Misc.ColorWhite, effectPercentage));
        _timer += Time.deltaTime;
    }
}
