using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Networking.API.Responses.DetailsV2;
using MelonLoader;

namespace Kafe.OSC.Utils;

internal static class ApiRequests
{
    internal static async Task<string> RequestAvatarDetailsPageTask(string guid)
    {
        MelonLogger.Msg($"[API] Fetching avatar {guid} name...");
        BaseResponse<ContentAvatarResponse> response;
        try
        {
            var payload = new { avatarID = guid };
            response = await ApiConnection.MakeRequest<ContentAvatarResponse>(ApiConnection.ApiOperation.AvatarDetail, payload, "2");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[API] Fetching avatar {guid} name has Failed! Location: OSC.Utils.ApiRequests.cs");
            MelonLogger.Error(ex);
            return null;
        }

        if (response == null)
        {
            MelonLogger.Msg($"[API] Fetching avatar {guid} name has Failed! Response came back empty.");
            return null;
        }

        MelonLogger.Msg($"[API] Fetched avatar {guid} name successfully! Name: {response.Data.Name}");
        Events.Avatar.OnAvatarDetailsReceived(response.Data.Id, response.Data.Name);
        return response.Data.Name;
    }
}
