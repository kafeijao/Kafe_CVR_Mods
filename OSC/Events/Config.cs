using System;

namespace OSC.Events; 

internal static class Config {
    
    internal static event Action<int, int> InPortChanged;
    internal static event Action<string, string> OutIpChanged;
    internal static event Action<int, int> OutPortChanged;
    
    internal static void OnInPortChanged(int oldValue, int newValue) {
        InPortChanged?.Invoke(oldValue, newValue);
    }
    
    internal static void OnOutIpChanged(string oldValue, string newValue) {
        OutIpChanged?.Invoke(oldValue, newValue);
    }
    
    internal static void OnOutPortChanged(int oldValue, int newValue) {
        OutPortChanged?.Invoke(oldValue, newValue);
    }
}