using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using MelonLoader;

namespace Kafe.RequestLib;

/// <summary>
/// The only class you should use to Interact with RequestLib.
/// </summary>
public static class API {

    /// <summary>
    /// The possible results of a Request.
    /// </summary>
    public enum RequestResult {
        /// <summary>
        /// The request didn't get an answer and timed out. Requests time out after 1 minute.
        /// </summary>
        TimedOut,
        /// <summary>
        /// The request was accepted!
        /// </summary>
        Accepted,
        /// <summary>
        /// The request was declined :(
        /// </summary>
        Declined,
    }

    /// <summary>
    /// Represents a request of RequestLib.
    /// </summary>
    public class Request {

        /// <summary>
        /// Gets the Guid of the source player from whom the request is originating.
        /// </summary>
        public readonly string SourcePlayerGuid;

        /// <summary>
        /// Gets the Guid of the target player to whom the request is being sent.
        /// </summary>
        public readonly string TargetPlayerGuid;

        /// <summary>
        /// Gets the message to be displayed on the request.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// Gets the optional metadata that will be available in the Interceptor.
        /// </summary>
        public readonly string Metadata;

        internal readonly string ModName;
        internal readonly DateTime Timeout;
        internal readonly Action<Request, Response> OnResponse;

        /// <summary>
        /// Constructor of requests, should be used by the Mods to create a request.
        /// </summary>
        /// <param name="targetPlayerGuid">Target player guid</param>
        /// <param name="message">The message to be displayed on the request.</param>
        /// <param name="onResponse">The Action that will be called when the player replies or the request times out</param>
        /// <param name="metadata">Optional metadata that will be available in the Interceptor.</param>
        public Request(string targetPlayerGuid, string message, Action<Request, Response> onResponse, string metadata = "") {
            TargetPlayerGuid = targetPlayerGuid;
            Message = message;
            OnResponse = onResponse;

            Metadata = metadata;

            ModName = RequestLib.GetModName();
            SourcePlayerGuid = MetaPort.Instance.ownerId;
            Timeout = DateTime.UtcNow + RequestTimeout;
        }

        internal Request(string modName, DateTime timeout, string sourcePlayerGuid, string targetPlayerGuid, string message, string metadata) {
            ModName = modName;
            Timeout = timeout;
            SourcePlayerGuid = sourcePlayerGuid;
            TargetPlayerGuid = targetPlayerGuid;
            Message = message;
            Metadata = metadata;
        }
    }


    /// <summary>
    /// Represents a response of RequestLib.
    /// </summary>
    public class Response {

        /// <summary>
        /// Gets the result of the request. The result can be TimedOut, Accepted, or Declined.
        /// </summary>
        public readonly RequestResult Result;

        /// <summary>
        /// Gets the optional metadata related to the response. This metadata might provide additional information about the response.
        /// </summary>
        public readonly string Metadata;

        /// <summary>
        /// Internal constructor of responses. Should not be used by Mods!
        /// </summary>
        internal Response(RequestResult result, string metadata) {
            Result = result;
            Metadata = metadata;
        }
    }

    /// <summary>
    /// Wrapper to hold the information of the Result of an Interceptor.
    /// </summary>
    public class InterceptorResult {

        internal readonly bool ShouldDisplayRequest;
        internal readonly RequestResult ResponseResult;
        internal readonly string ResponseMetadata;

        /// <summary>
        /// Generates a response for the case when you want to prevent the display of a request. This means the request
        /// won't appear for the user to reply.
        /// </summary>
        /// <param name="resultOverride">The result that will be sent to the request. Defaults to not sending anything (Time out).</param>
        /// <param name="responseMetadata">Optional metadata that can be retrieved by the request initiator.</param>
        /// <returns>An InterceptorResult for you to use on your Interceptor function.</returns>
        public static InterceptorResult GetPreventShowingRequest(RequestResult resultOverride = RequestResult.TimedOut, string responseMetadata = "") {
            return new InterceptorResult(false, resultOverride, responseMetadata);
        }

        /// <summary>
        /// Generates a response for the case when you don't want to prevent the display of a request. This means a request
        /// will still appear for the user to reply to.
        /// </summary>
        /// <returns>An InterceptorResult for you to use on your Interceptor function.</returns>
        public static InterceptorResult GetShowRequest() {
            return new InterceptorResult(true);
        }

        private InterceptorResult(bool shouldDisplayRequest, RequestResult responseResult = RequestResult.TimedOut, string responseMetadata = "") {
            ShouldDisplayRequest = shouldDisplayRequest;
            ResponseResult = responseResult;
            ResponseMetadata = responseMetadata;
        }
    }

    /// <summary>
    /// Player info accessible when when we get a Remote Player's info update. Happens whenever someone with the RequestLib joins the Instance.
    /// </summary>
    public class PlayerInfo {

        /// <summary>
        /// The username of this Remote Player.
        /// </summary>
        public readonly string Username;

        /// <summary>
        /// The GUID of this Remote Player.
        /// </summary>
        public readonly string Guid;

        /// <summary>
        /// Check if the Remote Player has the RequestLib installed.
        /// </summary>
        /// <returns>Whether this Remote Player has the Request Library Installed or not.</returns>
        public bool HasRequestLib() => API.HasRequestLib(Guid);

        /// <summary>
        /// Check if the Remote Player has your mod Installed.
        /// </summary>
        /// <returns>Whether this Remote Player has your mod install or not.</returns>
        public bool HasMod() => API.HasRequestLib(Guid) && RemotePlayerMods[Guid].Contains(RequestLib.GetModName());

        internal PlayerInfo(string guid) {
            Guid = guid;
            Username = CVRPlayerManager.Instance.TryGetPlayerName(guid);
        }
    }

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);
    internal static readonly Dictionary<string, Func<Request, InterceptorResult>> RegisteredMods = new();
    internal static readonly Dictionary<string, string[]> RemotePlayerMods = new();

    /// <summary>
    /// Called whenever we receive the information about the Mods of a remote player in the current Instance.
    /// This is useful if you need to update data of a player of whether they have RequestLib or your Mod installed.
    /// </summary>
    public static Action<PlayerInfo> PlayerInfoUpdate;

    /// <summary>
    /// Registers your Mod from the RequestLib. You should run this during the initialization of your mod.
    /// </summary>
    public static void RegisterMod(Func<Request, InterceptorResult> interceptor = null) {
        var modName = RequestLib.GetModName();
        if (RegisteredMods.ContainsKey(modName)) {
            MelonLogger.Warning($"[RegisterMod] {modName} is already Registered! Ignoring...");
            return;
        }
        RegisteredMods.Add(modName, interceptor);
    }

    /// <summary>
    /// Sends a request to a remote player in the Instance.
    /// </summary>
    /// <param name="request">Instance of the request you want to send. Use it's constructor to create one.</param>
    public static void SendRequest(Request request) => ModNetwork.SendRequest(request);

    /// <summary>
    /// Checks whether a remote player in the current Instance has the RequestLib or not.
    /// </summary>
    /// <param name="playerGuid">The Remote Player Guid you want to check.</param>
    /// <returns>Whether the remote player has the RequestLib installed or not.</returns>
    public static bool HasRequestLib(string playerGuid) {
        return !ModNetwork.IsOfflineInstance() && RemotePlayerMods.ContainsKey(playerGuid);
    }

    /// <summary>
    /// Checks whether a remote player in the current Instance has your Mod or not.
    /// </summary>
    /// <param name="playerGuid">The Remote Player Guid you want to check.</param>
    /// <returns>Whether the remote player has your mod installed or not.</returns>
    public static bool HasMod(string playerGuid) {
        return HasRequestLib(playerGuid) && RemotePlayerMods[playerGuid].Contains(RequestLib.GetModName());
    }

    /// <summary>
    /// Retrieve the currently pending sent requests.
    /// This might be useful if you want to cancel a request, but you didn't save it previously.
    /// </summary>
    /// <returns>The currently pending requests sent by your mod.</returns>
    public static HashSet<Request> GetPendingSentRequests() => ModNetwork.GetPendingSentRequests(RequestLib.GetModName());

    /// <summary>
    /// Cancels a pending request you sent.
    /// </summary>
    /// <param name="request">The request reference.</param>
    public static void CancelSentRequest(Request request) => ModNetwork.CancelSentRequest(request);

    /// <summary>
    /// Get the currently pending received requests.
    /// This might be useful if you want to answer to the requests via the mod, but you don't want to use an interceptor.
    /// </summary>
    /// <returns>The currently pending request received from your mod from a remote player.</returns>
    public static HashSet<Request> GetPendingReceivedRequests() => ModNetwork.GetPendingReceivedRequests(RequestLib.GetModName());

    /// <summary>
    /// Resolves manually a currently pending request you received.
    /// </summary>
    /// <param name="request">The request reference, you can get it from GetPendingReceivedRequests.</param>
    /// <param name="result">Which answer should be sent as the response.</param>
    /// <param name="metadata">Optional metadata if you want to send extra info to the requester.</param>
    public static void ResolveReceivedRequest(Request request, RequestResult result, string metadata = "") {
        ModNetwork.ResolveReceivedRequest(request, result, metadata);
    }

    internal static InterceptorResult RunInterceptor(Request request) {

        // If there are no interceptors for this request => display the request
        if (!RegisteredMods.TryGetValue(request.ModName, out var interceptor) || interceptor == null) {
            return InterceptorResult.GetShowRequest();
        }

        // Otherwise run the interceptor and let it decide whether to display the request to the target user or not.
        try {
            return interceptor.Invoke(request);
        }
        catch (Exception e) {
            MelonLogger.Warning($"[RunInterceptor] There was an error running the interceptor for the mod {request.ModName}!");
            MelonLogger.Error(e);
        }

        return InterceptorResult.GetShowRequest();
    }
}
