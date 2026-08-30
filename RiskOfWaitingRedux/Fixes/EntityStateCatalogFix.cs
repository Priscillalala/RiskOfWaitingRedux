using HarmonyLib;
using RoR2;
using RoR2.ContentManagement;
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
        // yield one or two times to maintain a consistent framerate
        // this is the equivalent of yielding after every 600 configurations are applied, since the original coroutine already yields 1 in 10
        const int MAX_YIELD_ATTEMPTS_PER_FRAME = 80;
        int yieldAttemptsThisFrame = 0;
        while (result.MoveNext())
        {
            if (++yieldAttemptsThisFrame > MAX_YIELD_ATTEMPTS_PER_FRAME)
            {
                yieldAttemptsThisFrame = 0;
                yield return null;
            }
        }
    }
}
