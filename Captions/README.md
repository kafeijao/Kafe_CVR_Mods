# Captions (Plugin)

[![Download Latest Captions.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest Captions.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/Captions.dll)

Use [whisper.cpp](https://github.com/ggml-org/whisper.cpp) to populate ChatBoxes with player's captions of their voice.
It does its best attempt to translate foreign languages to English, although not very well.

**⚠️NOTE0⚠️:** THIS IS A **PLUGIN**, NOT MOD, IT GOES IN THE `Plugins` folder!

**⚠️NOTE1⚠️:** This plugin is heavy on performance, both GPU, GPU and Memory!

**⚠️NOTE2⚠️:** Is this still very much a rough implementation, expect lack of polish and issues

In the quick menu, you have the Captions settings:

- Enable/Disable the plugin
- Download Whisper models
- Select the current active model
- Control who it should process the voice
- Fine control the audio detection (these are just for testing, they are nor persistent)

In the melon prefs, you have some extra Captions settings:

- Enable Logs
- Change the CPU thread count (currently it uses half of the available system thread count)
- Skip the native binary extraction (not recommended unless you want to provide your own)

## Supported models

You can use any whisper.cpp model from: https://huggingface.co/ggerganov/whisper.cpp/tree/main

The models should be placed in `\UserData\Captions\Models`, that `UserData` folder should be next to the `Mods` folder

I heavily recommend the `ggml-large-v3-turbo.bin` model, it's the best for accuracy while still being fast

## Update Whisper Net

1. Update [kafeijao's whisper.net fork on captions branch](<https://github.com/kafeijao/whisper.net/tree/captions>)
2. Run `git submodule update --remote`

## Credits

- Thanks [Noachi](https://github.com/NoachiCode) for suggesting revisiting this plugin as there was now a faster
  revision of the model (turbo v3). Note to self: I should stop listening to your suggestions as it always ends up in
  pain and misery, it's like a curse
- Thanks [Bono](https://github.com/ddakebono) for suggesting trying becoming a MelonLoader `Plugin` to address broken
  native dll
- <https://github.com/Macoron/whisper.unity>
- <https://github.com/sandrohanea/whisper.net>
