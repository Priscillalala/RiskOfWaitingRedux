using HarmonyLib;
using RoR2;
using System.Collections;

namespace RiskOfWaitingRedux.Fixes;

// The EntityStateCatalog yields too often when applying entity state configurations
// Fix: wrap the original coroutine & block the majority of yield attempts
public static class EntityStateCatalogFix
{
    public static void Init()
    {
        Plugin.Harmony.PatchAll(typeof(EntityStateCatalogFix));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(EntityStateCatalog), nameof(EntityStateCatalog.SetElements))]
    private static IEnumerator SetElementsNoYielding(IEnumerator result)
    {
        Plugin.Logger.LogMessage("EntityStateCatalogFix is happening!");
        // yield one or two times to maintain a consistent framerate
        const int MAX_YIELD_ATTEMPTS_PER_FRAME = 75;
        int yieldAttemptsThisFrame = 0;
        while (result.MoveNext())
        {
            if (++yieldAttemptsThisFrame > MAX_YIELD_ATTEMPTS_PER_FRAME)
            {
                yieldAttemptsThisFrame = 0;
                Plugin.Logger.LogWarning("EntityStateCatalogFix is yielding!");
                yield return null;
            }
        }
    }
}
