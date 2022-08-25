using JetBrains.Annotations;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Entities;

public abstract class ParameterEntry {
    public abstract void Update();

    public static readonly LinkedList<ParameterEntry> Entries = new();

    public static void Add(Animator animator, AnimatorControllerParameter param, TextMeshProUGUI display) {
        switch (param.type) {
            case AnimatorControllerParameterType.Float:
                Entries.AddLast(new ParameterEntry<float>(param, display, animator.GetFloat));
                return;
            case AnimatorControllerParameterType.Int:
                Entries.AddLast(new ParameterEntry<int>(param, display, animator.GetInteger));
                return;
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                Entries.AddLast(new ParameterEntry<bool>(param, display, animator.GetBool));
                return;
                //Entries.AddLast(new ParameterEntry<bool>(param, display, animator.GetBool, true));
                //return;
        }
    }
}

internal class ParameterEntry<T> : ParameterEntry {

    private int Hash;
    private TextMeshProUGUI Display;
    private Func<int, T> GetParameterFunc;

    // Trigger related stuff
    // private bool IsTrigger;
    // private bool WasTriggered;
    // private float TriggerCooldown;

    public ParameterEntry(AnimatorControllerParameter param, TextMeshProUGUI display, Func<int, T> getParameterFunc, bool isTrigger = false) {
        Hash = param.nameHash;
        Display = display;
        GetParameterFunc = getParameterFunc;
        //IsTrigger = isTrigger;
    }

    public override void Update() {
        Display.text = GetParameterFunc.Invoke(Hash).ToString();

        // Trigger delay to fade implementation
        // Removed because people might think their animator is broken because the triggers last for so long
        /*
        var value = GetParameterFunc.Invoke(Hash);
        Display.text = value.ToString();

        // Make triggers stay true for a while
        if (IsTrigger && value is bool triggered) {
            if (WasTriggered) {
                TriggerCooldown -= Time.deltaTime;
                Display.text = true.ToString();
                if (TriggerCooldown <= 0f) {
                    WasTriggered = false;
                }
            }
            if (triggered) {
                WasTriggered = true;
                TriggerCooldown = 0.25f;
                Display.text = true.ToString();
            }
        }
        */
    }
}