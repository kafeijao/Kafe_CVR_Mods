using UnityEngine;

namespace Kafe.CCK.Debugger.Entities;

public abstract class AnimatorParameterUtils
{

    public static Func<string> StringValueGetter(Animator animator, AnimatorControllerParameter param)
    {
        // Returns a function that returns the value of the Parameter as a string.
        switch (param.type)
        {
            case AnimatorControllerParameterType.Float:
                return () => animator.GetFloat(param.nameHash).ToString();
            case AnimatorControllerParameterType.Int:
                return () => animator.GetInteger(param.nameHash).ToString();
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                return () => animator.GetBool(param.nameHash).ToString();
        }
        return null;
    }
}
