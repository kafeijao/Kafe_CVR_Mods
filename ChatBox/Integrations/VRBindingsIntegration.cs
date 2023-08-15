using Valve.VR;

namespace Kafe.ChatBox.Integrations;

public class VRBindingsIntegration {

    internal static void Initialize() {
        VRBinding.VRBindingMod.RegisterBinding("ChatBoxSendMsg", "Send ChatBox Message", VRBinding.VRBindingMod.Requirement.optional, a => {
            if (a.GetStateDown(SteamVR_Input_Sources.Any)) {
                API.OpenKeyboard();
            }
        });
    }
}
