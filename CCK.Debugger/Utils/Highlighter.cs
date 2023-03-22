using ABI_RC.Core.Savior;
using HighlightPlus;
using UnityEngine;

namespace Kafe.CCK.Debugger.Utils; 

public static class Highlighter {
    
    private static GameObject lastTarget;
    private static HighlightProfile debugHighlight;
    
    static Highlighter() {
        debugHighlight = UnityEngine.Object.Instantiate(MetaPort.Instance.worldHighlightProfile);
        // Todo: Customize the highlight
        //var blue = new Color(0f, 0.69f, 1f);
    }
    
    public static void ClearTargetHighlight() {
        if (lastTarget == null) return;
        
        HighlightEffect lastHighlightEffect = lastTarget.GetComponent<HighlightEffect>();
        if (lastHighlightEffect == null) return;
        lastHighlightEffect.SetHighlighted(false);
        lastTarget = null;
    }
    
    public static void SetTargetHighlight(GameObject target) {
        
        // Clear previous highlight
        if (lastTarget != null) ClearTargetHighlight();
        
        // Ignore if the target sent is null
        if (target == null) return;
        
        // Attempt to re-use last highlight effect (if still on), otherwise create a new one
        HighlightEffect highlightEffect = target.GetComponent<HighlightEffect>();
        if (highlightEffect == null)
            highlightEffect = target.AddComponent<HighlightEffect>();
        
        // Setup the highlight and turn it on
        highlightEffect.ProfileLoad(debugHighlight);
        highlightEffect.Refresh();
        highlightEffect.SetHighlighted(true);
        
        // Mark the current target as the previous target
        lastTarget = target;
    }
}