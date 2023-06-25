using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using cohtml;
using HarmonyLib;
using MelonLoader;

namespace Kafe.RequestLib;

internal static class CohtmlPatches {

    public static Action HasNotifications;

    private static bool _cvrTestPatched;
    private static bool _cvrUIPatched;

    private static void UpdateRequests() {
        UpdateMainMenuRequests();
        // this will call our UpdateInvites patch (we need to prevent double notifications from appearing)
        CohtmlHud.Instance.UpdateNotifierStatus();
    }

    private static void UpdateMainMenuRequests() {
        if (!_cvrTestPatched) {
            MelonLogger.Warning(
                $"[DisplayRequest][CVRTest] Attempted to display a request, but the view was not initialized.");
        }
        else {
            ViewManager.Instance.gameMenuView.View.TriggerEvent("RequestLibModRequestsUpdate", Request.GetRequests());
        }
    }

    internal class Request {

        private static readonly Dictionary<string, Request> Requests = new();
        private static readonly List<Option> DefaultOptions = new() {
            new Option(OptionType.Accept),
            new Option(OptionType.Decline),
            new Option(OptionType.Cancel),
        };

        internal enum OptionType {
            Accept,
            Decline,
            Cancel,
        }

        internal struct Option {

            public string Name;
            public string Image;

            public Option(OptionType type) {
                Name = type.ToString();
                Image = type switch {
                    OptionType.Accept => "accept",
                    OptionType.Decline => "deny",
                    OptionType.Cancel => "remove",
                    _ => null,
                };
            }
        }

        public string ID;
        public string SenderName;
        public string Name;
        public string Text;
        public string TextQM = "Press on Show all to see the whole Msg";
        public List<Option> Options = DefaultOptions;

        internal static List<Request> GetRequests() => Requests.Values.Reverse().ToList();
        internal static int Count() => Requests.Count;

        internal static Request DeleteRequest(string id) {
            if (!Requests.TryGetValue(id, out var req)) return null;
            Requests.Remove(id);
            UpdateRequests();
            return req;
        }

        internal static void CreateRequest(string guid, string senderGuid, string modName, string message) {
            var req = new Request {
                ID = guid,
                SenderName = CVRPlayerManager.Instance.TryGetPlayerName(senderGuid),
                Name = modName,
                Text = message,
            };
            Requests[guid] = req;
            UpdateRequests();
        }

        internal static void OnResponse(string requestId, string result) {

            var request = DeleteRequest(requestId);
            if (request == null) return;

            Enum.TryParse<OptionType>(result, true, out var responseType);
            switch (responseType) {
                case OptionType.Accept:
                    ModNetwork.SendResponse(request.ID, true, "");
                    break;
                case OptionType.Decline:
                    ModNetwork.SendResponse(request.ID, false, "");
                    break;
            }
        }

        private Request() { }
    }



    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPriority(Priority.LowerThanNormal)]
        [HarmonyPatch(typeof(CohtmlHud), nameof(CohtmlHud.UpdateNotifierStatus))]
        public static void After_CohtmlHud_UpdateNotifierStatus(CohtmlHud __instance) {
            // Update the hud notification little icon
            try {
                if (!__instance._isReady ||
                    !MetaPort.Instance.settings.GetSettingsBool("HUDCustomizationInvites")) return;
                var hasNotification = ViewManager.Instance.Invites.Count > 0
                                      || ViewManager.Instance.InviteRequests.Count > 0
                                      || Request.Count() > 0;
                __instance.hudView.View.TriggerEvent("ChangeHudStatus", "notifications", hasNotification);
                if (hasNotification) HasNotifications?.Invoke();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CohtmlHud_UpdateNotifierStatus)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.Start))]
        public static void After_ViewManager_Start(ViewManager __instance) {
            // Patch the Main Menu to tap in the Messages
            try {
                // Load the bindings
                __instance.gameMenuView.Listener.ReadyForBindings += () => {
                    __instance.gameMenuView.View.BindCall("RequestLibModResponse", Request.OnResponse);
                };

                // Inject our Cohtml
                __instance.gameMenuView.Listener.FinishLoad += _ => {
                    __instance.gameMenuView.View.ExecuteScript(ModConfig.CVRTestJSPatchesContent);

                    // Mark as initialized
                    _cvrTestPatched = true;

                    // Set an initial update
                    UpdateMainMenuRequests();
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        public static void After_CVR_MenuManager_Start(CVR_MenuManager __instance) {
            // Patch the Quick Menu to tap in the Messages
            try {
                // Load the bindings
                __instance.quickMenu.Listener.ReadyForBindings += () => {
                    __instance.quickMenu.View.BindCall("RequestLibModResponse", Request.OnResponse);
                };

                // Inject our Cohtml
                __instance.quickMenu.Listener.FinishLoad += _ => {
                    __instance.quickMenu.View.ExecuteScript(ModConfig.CVRUIJSPatchesContent);

                    // Mark as initialized
                    _cvrUIPatched = true;

                    // Set an initial update (this will call our UpdateInvites patch)
                    CohtmlHud.Instance.UpdateNotifierStatus();
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.UpdateInvites))]
        public static void After_CVR_MenuManager_UpdateInvites(CVR_MenuManager __instance) {
            // Patch the update invites from the quick menu, so we can update our stuff after
            try {
                if (!__instance._quickMenuReady || Request.Count() <= 0) return;
                if (!_cvrUIPatched) {
                    MelonLogger.Warning(
                        $"[DisplayRequest][CVRUI] Attempted to display a request, but the view was not initialized.");
                }
                else {
                    CVR_MenuManager.Instance.quickMenu.View.TriggerEvent("RequestLibModRequestsUpdate", Request.GetRequests());
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_UpdateInvites)}");
                MelonLogger.Error(e);
            }

        }
    }
}
