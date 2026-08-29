using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HG.Reflection;
using MonoMod.Utils;
using RoR2;
using RoR2.Achievements;
using RoR2.EntitlementManagement;
using RoR2.UI.LogBook;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RiskOfWaitingRedux.Fixes;

// too much yielding during logbook asset loading
public static class LogBookInitFix
{
    public static void Init()
    {
        /*foreach (string textureKey in LogBookController.CommonAssets._textures.Keys)
        {
            LegacyResourcesAPI.LoadAsync<Texture2D>(textureKey);
        }
        foreach (string gameObjectKey in LogBookController.CommonAssets._gameObjects.Keys)
        {
            LegacyResourcesAPI.LoadAsync<GameObject>(gameObjectKey);
        }*/
        //On.RoR2.UI.LogBook.LogBookController.CommonAssets.Init += CommonAssets_Init;
        RiskOfWaitingReduxPlugin.Harmony.PatchAll(typeof(LogBookInitFix));
    }

    [HarmonyPrefix, HarmonyPatch(typeof(LogBookController), nameof(LogBookController.CommonAssets.Init))]
    private static bool ReplaceCommonAssetsInit(ref IEnumerator __result)
    {
        __result = FasterCommonAssetsInit();
        return false;
    }

    private static IEnumerator FasterCommonAssetsInit()
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"starting LogBookController.CommonAssets.Init");
        Stopwatch stopwatch = new();
        stopwatch.Start();
        foreach (KeyValuePair<string, Action<Texture2D>> kvp in LogBookController.CommonAssets._textures)
        {
            AsyncOperationHandle<Texture2D> loadOpHandle = LegacyResourcesAPI.LoadAsync<Texture2D>(kvp.Key);
            yield return loadOpHandle;
            kvp.Value(loadOpHandle.Result);
        }
        foreach (KeyValuePair<string, Action<GameObject>> kvp2 in LogBookController.CommonAssets._gameObjects)
        {
            AsyncOperationHandle<GameObject> loadOpHandle2 = LegacyResourcesAPI.LoadAsync<GameObject>(kvp2.Key);
            yield return loadOpHandle2;
            kvp2.Value(loadOpHandle2.Result);
        }
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"finished LogBookController.CommonAssets.Init at {stopwatch.ElapsedMilliseconds}ms");
    }
}
