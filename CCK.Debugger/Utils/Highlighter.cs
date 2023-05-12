using System.Collections;
using ABI_RC.Core.Savior;
using HighlightPlus;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger.Utils; 

public static class Highlighter {

    private static object _clearTimeoutCancelToken;
    
    private static GameObject _lastTarget;
    private static readonly HighlightProfile DebugHighlight;


    private static readonly Color Blue = new Color(0f, 0.69f, 1f);
    
    static Highlighter() {
        DebugHighlight = UnityEngine.Object.Instantiate(MetaPort.Instance.worldHighlightProfile);
    }

    private static IEnumerator ClearTimeout() {
        yield return new WaitForSeconds(5f);
        _clearTimeoutCancelToken = null;
        ClearTargetHighlight();
    }
    
    public static void ClearTargetHighlight() {

        // Clear timeout if there was one going
        if (_clearTimeoutCancelToken != null) {
            MelonCoroutines.Stop(_clearTimeoutCancelToken);
            _clearTimeoutCancelToken = null;
        }

        if (_lastTarget == null) return;
        
        HighlightEffect lastHighlightEffect = _lastTarget.GetComponent<HighlightEffect>();
        if (lastHighlightEffect == null) return;
        lastHighlightEffect.SetHighlighted(false);
        _lastTarget = null;
    }
    
    public static void SetTargetHighlight(GameObject target) {
        
        // Clear previous highlight
        if (_lastTarget != null) ClearTargetHighlight();
        
        // Ignore if the target sent is null
        if (target == null) return;
        
        // Attempt to re-use last highlight effect (if still on), otherwise create a new one
        var highlightEffect = target.GetComponent<HighlightEffect>();
        if (highlightEffect == null) {
            highlightEffect = target.AddComponent<HighlightEffect>();
        }

        // Setup the highlight and turn it on
        highlightEffect.ProfileLoad(DebugHighlight);
        highlightEffect.Refresh();
        highlightEffect.SetHighlighted(true);

        // Set color
        highlightEffect.SetGlowColor(Blue);
        
        // Mark the current target as the previous target
        _lastTarget = target;

        // Clear pending clear coroutine and queue another one
        if (_clearTimeoutCancelToken != null) MelonCoroutines.Stop(_clearTimeoutCancelToken);
        _clearTimeoutCancelToken = MelonCoroutines.Start(ClearTimeout());
    }
}
