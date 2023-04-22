using MelonLoader;

namespace Kafe.ChatBox;

public class ChatBox : MelonMod {

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
    }

}
