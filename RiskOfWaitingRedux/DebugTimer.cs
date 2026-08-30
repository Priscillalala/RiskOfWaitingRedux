#if DEBUG
using HarmonyLib;
using RoR2;
using System.Diagnostics;

namespace RiskOfWaitingRedux;

public static class DebugTimer
{
    private static Stopwatch totalLoadStopwatch;
    private static Stopwatch printLoadStopwatch;

    public static void Init()
    {
        RoR2Application.onLoad += OnLoadFinished;
        RiskOfWaitingReduxPlugin.Harmony.PatchAll(typeof(DebugTimer));

    }

    private static void OnLoadStart()
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"Start RoR2 load");
        totalLoadStopwatch = new();
        totalLoadStopwatch.Start();
        printLoadStopwatch = new();
        printLoadStopwatch.Start();
    }

    private static void OnLoadFinished()
    {
        totalLoadStopwatch.Stop();
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"Finish RoR2 load in {totalLoadStopwatch.ElapsedMilliseconds}ms");
        totalLoadStopwatch = null;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(RoR2Application), nameof(RoR2Application.PrintSW))]
    private static void LogPrintSW(string message)
    {
        if (printLoadStopwatch == null)
        {
            RiskOfWaitingReduxPlugin.Logger.LogError("Print load null!");
        }
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"{message ?? "null"} {printLoadStopwatch.ElapsedMilliseconds}ms since last print");
        printLoadStopwatch.Restart();
    }

    [HarmonyPrefix, HarmonyPatch(typeof(RoR2Application), nameof(RoR2Application.EnableBehaviours))]
    private static void LogEnableBehaviours(RoR2Application __instance)
    {
        Stopwatch stopwatch = new();
        int num = __instance.BehavioursToEnableDuringStartup.Length;
        for (int i = 0; i < num; i++)
        {
            //if (self.BehavioursToEnableDuringStartup[i].GetType().Name == "PostProcessVolume")
            //{
            //    continue;
            //}
            stopwatch.Restart();
            __instance.BehavioursToEnableDuringStartup[i].enabled = true;
            RiskOfWaitingReduxPlugin.Logger.LogMessage($"Behaviour {__instance.BehavioursToEnableDuringStartup[i].GetType().Name} initialized in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(RoR2Application), nameof(RoR2Application.Awake))]
    private static void OnAwake()
    {
        OnLoadStart();
    }
}
#endif