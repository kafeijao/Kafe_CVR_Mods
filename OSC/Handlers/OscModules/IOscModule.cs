namespace OSC.Handlers.OscModules;

public abstract class OscHandler {
    internal abstract void Enable();
    internal abstract void Disable();
    internal virtual void ReceiveMessageHandler(string address, List<object> args) {}
}
