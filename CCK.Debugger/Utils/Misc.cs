using UnityEngine;

namespace CCK.Debugger.Utils; 

public static class Misc {

    public static string ParseParameter(AnimatorControllerParameter param, float value) {
        
        switch (param.type) {
            case AnimatorControllerParameterType.Float:
                return value.ToString();
            case AnimatorControllerParameterType.Int:
                return Math.Round(value, MidpointRounding.ToEven).ToString();
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                return (value > 0.5f) ? "True" : "False";
            default:
                throw new ArgumentOutOfRangeException();
        }

    }
    
}