using MelonLoader;
using UnityEngine;

namespace OSC.Utils;

public static class Parameters {

    public static AnimatorControllerParameterType? GetParameterType(object valueObj) {

        // Sort their types and call the correct handler
        if (valueObj is float) return AnimatorControllerParameterType.Float;
        if (valueObj is int) return AnimatorControllerParameterType.Int;
        if (valueObj is bool) return AnimatorControllerParameterType.Bool;
        if (valueObj is null) return AnimatorControllerParameterType.Trigger;

        // Attempt to parse the string into their proper type and then call the correct handler
        if (valueObj is string valueStr) {
            if (string.IsNullOrEmpty(valueStr)) return AnimatorControllerParameterType.Trigger;
            if (valueStr.ToLower().Equals("true")) return AnimatorControllerParameterType.Bool;
            if (valueStr.ToLower().Equals("false")) return AnimatorControllerParameterType.Bool;
            if (int.TryParse(valueStr, out _)) return AnimatorControllerParameterType.Int;
            if (float.TryParse(valueStr, out _)) return AnimatorControllerParameterType.Float;
        }

        MelonLogger.Error($"The parameter value {valueObj} was not able to be parsed, it has incorrect" +
                                  $"format or the type {valueObj.GetType()} is not supported.");
        return null;
    }


}
