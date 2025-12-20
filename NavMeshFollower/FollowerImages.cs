using System.Collections.Concurrent;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Networking.API.Responses.DetailsV2;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using HarmonyLib;
using MelonLoader;

namespace Kafe.NavMeshFollower;

public static class FollowerImages
{
    private static readonly ConcurrentDictionary<string, string> PropImageUrls = new ConcurrentDictionary<string, string>();

    public static bool TryGetPropImageUrl(string guid, out string imageUrl)
    {
        if (PropImageUrls.TryGetValue(guid, out var url))
        {
            imageUrl = ImageCache.QueueProcessImage(url);
            return true;
        }
        imageUrl = string.Empty;
        return false;
    }

    public static void SetFollowerButtonImage(Button button, string guid)
    {
        if (TryGetPropImageUrl(guid, out var imageUrl))
        {
            button.ButtonIcon = imageUrl;
            return;
        }

        _ = Task.Run(() => SetFollowerButtonImageTask(button, guid));
    }

    private static async Task SetFollowerButtonImageTask(Button button, string guid)
    {
        var url = await GetPropImageUrl(guid);
        button.ButtonIcon = ImageCache.QueueProcessImage(url);
    }

    private static async Task<string> GetPropImageUrl(string guid)
    {
        BaseResponse<ContentSpawnableResponse> response =
            await ApiConnection.MakeRequest<ContentSpawnableResponse>(ApiConnection.ApiOperation.PropDetail,
                apiVersion: "2", data: new { id = guid });

        if (response != null && response.IsSuccessStatusCode && response.Data != null)
            return response.Data.Image.ToString();

        return "";
    }

    [HarmonyPatch]
    internal class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnableDetail_t), nameof(SpawnableDetail_t.PopulateFromApiResponse))]
        private static void After_SpawnableDetail_t_PopulateFromApiResponse(ContentSpawnableResponse spawnableDetails)
        {
            // Cache the prop's image url when populating it (bare minimum effort to reduce api calls)
            try
            {
                PropImageUrls[spawnableDetails.Id] = spawnableDetails.Image.ToString();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_SpawnableDetail_t_PopulateFromApiResponse)}", e);
            }
        }
    }
}
