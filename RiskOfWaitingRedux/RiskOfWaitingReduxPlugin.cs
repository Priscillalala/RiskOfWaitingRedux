global using Path = System.IO.Path;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RiskOfWaitingRedux.Fixes;

namespace RiskOfWaitingRedux;

[BepInPlugin(GUID, NAME, VERSION)]
public class RiskOfWaitingReduxPlugin : BaseUnityPlugin
{
    public const string
        GUID = "groovesalad." + NAME,
        NAME = "RiskOfWaitingRedux",
        VERSION = "2.0.0";

    public static new ManualLogSource Logger { get; private set; }
    public static Harmony Harmony { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;
        Harmony = new Harmony(GUID);
#if DEBUG
        DebugTimer.Init();
#endif
#if true
        PostProcessingCache.Init();
        SearchableAttributeCache.Init();
        ConVarsCache.Init();
        EntityStateCatalogFix.Init();
#endif
    }
}
