using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace RiskOfWaitingRedux.Fixes;

// When the PostProcessManager inits, it scans every type in EVERY assembly to find PostProcessEffectSettings types
// This is obviously very slow, and also loads otherwise unused assemblies like the legacy MMHOOK dll, which massively inflates load times
// Fix: Filter out MMHOOK assemblies; only search assemblies which directly depend on Unity.Postprocessing.Runtime (including itself)
// We also implement a cache for the type search, but it only *slightly* outperforms the properly filtered type search (~10ms for me)
// The cache is only worth it because we are hooking PostProcessManager.ReloadBaseTypes anyway
public static class PostProcessCache
{
    private static string cacheDirectory;

    public static void Init()
    {
        cacheDirectory = CacheHelpers.GetCacheDirectory("PostProcessingCache");
        Directory.CreateDirectory(cacheDirectory);
        Plugin.Harmony.PatchAll(typeof(PostProcessCache));
    }


    [HarmonyPrefix, HarmonyPatch(typeof(PostProcessManager), nameof(PostProcessManager.ReloadBaseTypes))]
    private static bool ReloadBaseTypesFromCache(PostProcessManager __instance)
    {
        __instance.CleanBaseTypes();

        Assembly postProcessingAssembly = typeof(PostProcessEffectSettings).Assembly;
        Assembly riskOfWaitingReduxAssembly = typeof(PostProcessCache).Assembly;
        HandleAssembly(__instance, postProcessingAssembly);
        
        string postProcessingAssemblyName = postProcessingAssembly.GetName().Name;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // This mod needs to depend on Unity.Postprocessing.Runtime, but it will never include PostProcessEffectSettings
            if (CacheHelpers.IsLikelyMMHOOKAssembly(assembly) || assembly == riskOfWaitingReduxAssembly)
            {
                continue;
            }
            foreach (var referenceAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (referenceAssemblyName.Name == postProcessingAssemblyName)
                {
                    HandleAssembly(__instance, assembly);
                    break;
                }
            }
        }
        return false;
    }

    private static void RegisterSettingsType(PostProcessManager ppManager, Type settingsType, PostProcessAttribute ppAttribute)
    {
        ppManager.settingsTypes.Add(settingsType, ppAttribute);
        PostProcessEffectSettings baseSettings = (PostProcessEffectSettings)ScriptableObject.CreateInstance(settingsType);
        baseSettings.SetAllOverridesTo(state: true, excludeEnabled: false);
        ppManager.m_BaseSettings.Add(baseSettings);
    }

    private static void HandleAssembly(PostProcessManager ppManager, Assembly assembly)
    {
        string cachePath = Path.Combine(cacheDirectory, assembly.FullName);


        if (!TryLoadFromCache(ppManager, assembly, cachePath))
        {
            List<Type> settingsTypes = RegisterSettingsTypesInAssembly(ppManager, assembly);
            CreateCache(assembly, cachePath, settingsTypes);
        }
    }

    private static void CreateCache(Assembly assembly, string cachePath, List<Type> settingsTypes)
    {
        using FileStream fileStream = File.OpenWrite(cachePath);
        using BinaryWriter writer = new BinaryWriter(fileStream);

        CacheHelpers.WriteAssemblyIdentifer(writer, assembly);


        CacheHelpers.WriteTypeCollection(writer, settingsTypes);
    }

    private static List<Type> RegisterSettingsTypesInAssembly(PostProcessManager ppManager, Assembly assembly)
    {
        List<Type> settingsTypes = [];
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(PostProcessEffectSettings)) || type.IsAbstract)
            {
                continue;
            }
            PostProcessAttribute ppAttribute = type.GetCustomAttribute<PostProcessAttribute>(false);
            if (ppAttribute == null)
            {
                continue;
            }

            settingsTypes.Add(type);
            RegisterSettingsType(ppManager, type, ppAttribute);
        }
        return settingsTypes;
    }

    private static bool TryLoadFromCache(PostProcessManager ppManager, Assembly assembly, string cachePath)
    {
        ICollection<Type> cachedSettingsTypes;

        try
        {
            if (!File.Exists(cachePath))
            {
                Plugin.Logger.LogError($"PostProcessing cache for {assembly.FullName} doesn't exist - creating new cache");
                return false;
            }
            using FileStream fileStream = File.OpenRead(cachePath);
            using BinaryReader reader = new BinaryReader(fileStream);

            if (CacheHelpers.ReadAssemblyWasModified(reader, assembly))
            {
                Plugin.Logger.LogError($"PostProcessing cache for {assembly.FullName} is outdated - creating new cache");
                return false;
            }

            cachedSettingsTypes = CacheHelpers.ReadTypeCollection(reader, assembly);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"PostProcessing cache for {assembly.FullName} is likely corrupted - creating new cache: {ex}");
            return false;
        }

        foreach (var settingsType in cachedSettingsTypes)
        {
            RegisterSettingsType(ppManager, settingsType, settingsType.GetCustomAttribute<PostProcessAttribute>(false));
        }
        return true;
    }
}
