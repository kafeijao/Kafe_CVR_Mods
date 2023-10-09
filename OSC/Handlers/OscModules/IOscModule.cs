using MelonLoader;
using Rug.Osc.Core;

namespace Kafe.OSC.Handlers.OscModules;

public abstract class OscHandler {
    internal abstract void Enable();
    internal abstract void Disable();
    private bool HasWarned { get; set; }

    internal virtual void ReceiveMessageHandler(OscMessage oscMsg) {
        if (HasWarned)
            return;
        MelonLogger.Msg("[Info] You attempted to send a message to a module that doesn't not allow receiving. " +
                        $"Address Attempted: {oscMsg.Address}");
        HasWarned = true;
    }
}
