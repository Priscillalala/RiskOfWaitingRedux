using HarmonyLib;
using RoR2;
using System.Collections;

namespace RiskOfWaitingRedux.Fixes;

public static class EntityStateCatalogFix
{
    public static void Init()
    {
        RiskOfWaitingReduxPlugin.Harmony.PatchAll(typeof(EntityStateCatalogFix));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(EntityStateCatalog), nameof(EntityStateCatalog.SetElements))]
    private static IEnumerator SetElementsNoYielding(IEnumerator result)
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage("EntityStateCatalogFix is happening!");
        const int MAX_YIELD_ATTEMPTS_PER_FRAME = 75;
        int yieldAttemptsThisFrame = 0;
        while (result.MoveNext())
        {
            if (++yieldAttemptsThisFrame > MAX_YIELD_ATTEMPTS_PER_FRAME)
            {
                yieldAttemptsThisFrame = 0;
                RiskOfWaitingReduxPlugin.Logger.LogWarning("EntityStateCatalogFix is yielding!");
                yield return null;
            }
        }
    }
}
