using ABI_RC.Core.Savior;
using MelonLoader;

namespace Kafe.RequestLib;

public static class API {

    /// <summary>
    /// The possible results of a Request.
    /// </summary>
    public enum RequestResult {
        TimedOut,
        Accepted,
        Declined,
    }

    /// <summary>
    /// Represents a request of RequestLib.
    /// </summary>
    public class Request {

        public readonly string SourcePlayerGuid;
        public readonly string TargetPlayerGuid;
        public readonly string Message;
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

        /// <summary>
        /// Internal constructor of requests. Should not be used by Mods!
        /// </summary>
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

        public readonly RequestResult Result;
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

        /// <summary>
        /// Internal constructor of interceptor results. Should not be used by Mods! Use the helper Methods instead.
        /// </summary>
        private InterceptorResult(bool shouldDisplayRequest, RequestResult responseResult = RequestResult.TimedOut, string responseMetadata = "") {
            ShouldDisplayRequest = shouldDisplayRequest;
            ResponseResult = responseResult;
            ResponseMetadata = responseMetadata;
        }
    }

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);
    internal static readonly Dictionary<string, Func<Request, InterceptorResult>> RegisteredMods = new();
    internal static readonly Dictionary<string, string[]> RemotePlayerMods = new();

    /// <summary>
    /// Called whenever we receive the information about the Mods of a remote player in the current Instance.
    /// This is useful if you need to update data of a player of whether they have RequestLib or your Mod installed.
    /// </summary>
    /// <param name="string">The remote player guid.</param>
    public static Action<string> PlayerInfoUpdate;

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
    /// <param name="request"></param>
    public static void SendRequest(Request request) => ModNetwork.SendRequest(request);

    /// <summary>
    /// Checks whether a remote player in the current Instance has the RequestLib or not.
    /// </summary>
    /// <param name="playerGuid"></param>
    /// <returns></returns>
    public static bool HasRequestLib(string playerGuid) {
        return !ModNetwork.IsOfflineInstance() && RemotePlayerMods.ContainsKey(playerGuid);
    }

    /// <summary>
    /// Checks whether a remote player in the current Instance has your Mod or not.
    /// </summary>
    /// <param name="playerGuid">The target player in the Instance.</param>
    /// <returns></returns>
    public static bool HasMod(string playerGuid) {
        return HasRequestLib(playerGuid) && RemotePlayerMods[playerGuid].Contains(RequestLib.GetModName());
    }

    /// <summary>
    /// Runs the Mod's interceptor is available.
    /// </summary>
    /// <param name="request">Request being processed.</param>
    /// <returns>The Interception result that which can dictate whether the request is shown or not. Additionally when
    /// the request is not shown, you can provide a result and optional metadata that will be available on the requester
    /// side afterwards.</returns>
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
