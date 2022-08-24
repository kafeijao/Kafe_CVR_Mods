using System;

namespace OSC.Events; 

public static class Scene {
    
    public static event Action InputManagerCreated;
    
    internal static void OnInputManagerCreated() {
        InputManagerCreated?.Invoke();
    }
}