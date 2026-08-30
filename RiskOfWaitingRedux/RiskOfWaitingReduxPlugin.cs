global using Path = System.IO.Path;
global using Plugin = RiskOfWaitingRedux.RiskOfWaitingReduxPlugin;
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
    public static string DataDirectory { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;
        Harmony = new Harmony(GUID);
        DataDirectory = Path.Combine(Environment.CurrentDirectory, NAME + "Data");
#if DEBUG
        DebugTimer.Init();
#endif
#if true
        PostProcessCache.Init();
        SearchableAttributeCache.Init();
        ConVarsCache.Init();
        EntityStateCatalogFix.Init();
#endif
    }
}
