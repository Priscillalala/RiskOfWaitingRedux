global using Path = System.IO.Path;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RiskOfWaitingRedux.Fixes;
using RoR2;
using System.Diagnostics;

namespace RiskOfWaitingRedux;

[BepInPlugin(GUID, NAME, VERSION)]
public class RiskOfWaitingReduxPlugin : BaseUnityPlugin
{
    public const string
        GUID = "groovesalad." + NAME,
        NAME = "RiskOfWaitingRedux",
        VERSION = "1.0.1";

    public static RiskOfWaitingReduxPlugin Instance { get; private set; }
    public static new ManualLogSource Logger { get; private set; }
    public static Harmony Harmony { get; private set; }
    public static string RuntimeDirectory { get; private set; }

    private static Stopwatch totalLoadStopwatch;
    private static Stopwatch printLoadStopwatch;

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;
        Harmony = new Harmony(GUID);
        RuntimeDirectory = Path.GetDirectoryName(Info.Location);

        Init();
        PostProcessingCache.Init();
        SearchableAttributeCache.Init();
        ConVarsCache.Init();
        EntityStateCatalogFix.Init();
        //LogBookInitFix.Init();
        /*TestingTheWaters.Init();
        PostProcessingFix.Init();
        AchievementManagerFix.Init();
        FasterLogbookInit.Init();
        EntityStateCatalogFix.Init();
        CacheSearchableAttribute.Init();
        CacheConVars.Init();*/

        On.RoR2.RoR2Application.EnableBehaviours += RoR2Application_EnableBehaviours;

    }

    private static void Init()
    {
        RoR2Application.onLoad += OnLoadFinished;
        On.RoR2.RoR2Application.Awake += RoR2Application_Awake;
        On.RoR2.RoR2Application.PrintSW += RoR2Application_PrintSW;
    }

    private static void OnLoadStart()
    {
        Logger.LogMessage($"Start RoR2 load");
        totalLoadStopwatch = new();
        totalLoadStopwatch.Start();
        printLoadStopwatch = new();
        printLoadStopwatch.Start();
    }

    private static void OnLoadFinished()
    {
        totalLoadStopwatch.Stop();
        Logger.LogMessage($"Finish RoR2 load in {totalLoadStopwatch.ElapsedMilliseconds}ms");
        totalLoadStopwatch = null;
    }

    private static void RoR2Application_PrintSW(On.RoR2.RoR2Application.orig_PrintSW orig, string message)
    {
        if (printLoadStopwatch == null)
        {
            Logger.LogError("Print load null!");
        }
        Logger.LogMessage($"{message ?? "null"} {printLoadStopwatch.ElapsedMilliseconds}ms since last print");
        printLoadStopwatch.Restart();
        orig(message);
    }

    private static void RoR2Application_EnableBehaviours(On.RoR2.RoR2Application.orig_EnableBehaviours orig, RoR2Application self)
    {
        Stopwatch stopwatch = new();
        int num = self.BehavioursToEnableDuringStartup.Length;
        for (int i = 0; i < num; i++)
        {
            //if (self.BehavioursToEnableDuringStartup[i].GetType().Name == "PostProcessVolume")
            //{
            //    continue;
            //}
            stopwatch.Restart();
            self.BehavioursToEnableDuringStartup[i].enabled = true;
            Logger.LogMessage($"Behaviour {self.BehavioursToEnableDuringStartup[i].GetType().Name} initialized in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    private static void RoR2Application_Awake(On.RoR2.RoR2Application.orig_Awake orig, RoR2Application self)
    {
        OnLoadStart();
        orig(self);
        /*if (RoR2Application.maxPlayers != 4 || (Application.genuineCheckAvailable && !Application.genuine))
        {
            RoR2Application.isModded = true;
        }
        UnityEngine.Object.DontDestroyOnLoad(self.gameObject);
        if ((bool)RoR2Application.instance)
        {
            UnityEngine.Object.Destroy(self.gameObject);
            return;
        }
        RoR2Application.instance = self;
        RoR2Application.AssignBuildId();
        RoR2Application.Print("buildId = " + RoR2Application.buildId);
        if (RoR2Application.AssemblyTypes == null)
        {
            RoR2Application.AssemblyTypes = Assembly.GetExecutingAssembly().GetTypes();
        }
        if (!RoR2Application.isLoading)
        {
            RoR2Application.isLoading = true;
            self.StartCoroutine(TestingTheWaters.CustonOnLoad(self));
        }
        MainMenuController.OnMainMenuInitialised += self.OnMainMenuControllerInitialized;
        BaseUserEntitlementTracker<LocalUser>.OnUserEntitlementsUpdated = (Action)Delegate.Combine(BaseUserEntitlementTracker<LocalUser>.OnUserEntitlementsUpdated, new Action(self.OnEntitlementsUpdated));
        BaseUserEntitlementTracker<LocalUser>.OnAllUserEntitlementsUpdated = (Action)Delegate.Combine(BaseUserEntitlementTracker<LocalUser>.OnAllUserEntitlementsUpdated, new Action(self.OnAllUsersEntitlementsUpdated));
        */
    }
}
