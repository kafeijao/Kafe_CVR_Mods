using UnityEngine;

namespace CCK.Debugger.Entities;

public abstract class ParameterEntrySection {
    public abstract string GetValue();

    public static ParameterEntrySection Get(Animator animator, AnimatorControllerParameter param) {
        switch (param.type) {
            case AnimatorControllerParameterType.Float:
                var entityFloat = new ParameterEntrySection<float>(param, id => animator.GetFloat(id));
                return entityFloat;
            case AnimatorControllerParameterType.Int:
                var entityInt = new ParameterEntrySection<int>(param, id => animator.GetInteger(id));
                return entityInt;
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                var entityBool = new ParameterEntrySection<bool>(param, id => animator.GetBool(id));
                return entityBool;
                //Entries.AddLast(new ParameterEntry<bool>(param, display, animator.GetBool, true));
                //return;
        }

        return null;
    }
}

internal class ParameterEntrySection<T> : ParameterEntrySection {

    private readonly int _hash;
    private readonly Func<int, T> _getParameterFunc;

    // Trigger related stuff
    // private bool IsTrigger;
    // private bool WasTriggered;
    // private float TriggerCooldown;

    public ParameterEntrySection(AnimatorControllerParameter param, Func<int, T> getParameterFunc, bool isTrigger = false) {
        _hash = param.nameHash;
        _getParameterFunc = getParameterFunc;
        //IsTrigger = isTrigger;
    }

    public override string GetValue() {
        var value = _getParameterFunc.Invoke(_hash);
        return value.ToString();

        // Trigger delay to fade implementation
        // Removed because people might think their animator is broken because the triggers last for so long
        /*
        var value = GetParameterFunc.Invoke(Hash);
        _section.text = value.ToString();

        // Make triggers stay true for a while
        if (IsTrigger && value is bool triggered) {
            if (WasTriggered) {
                TriggerCooldown -= Time.deltaTime;
                _section.text = true.ToString();
                if (TriggerCooldown <= 0f) {
                    WasTriggered = false;
                }
            }
            if (triggered) {
                WasTriggered = true;
                TriggerCooldown = 0.25f;
                _section.text = true.ToString();
            }
        }
        */
    }
}
