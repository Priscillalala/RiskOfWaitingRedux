using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace RiskOfWaitingRedux.Fixes;

public static class PostProcessingCache
{
    private static string cacheDirectory;

    public static void Init()
    {
        cacheDirectory = CacheHelpers.GetCacheDirectory("PostProcessingCache");
        Directory.CreateDirectory(cacheDirectory);
        RiskOfWaitingReduxPlugin.Harmony.PatchAll(typeof(PostProcessingCache));
    }


    [HarmonyPrefix, HarmonyPatch(typeof(PostProcessManager), nameof(PostProcessManager.ReloadBaseTypes))]
    private static bool ReloadBaseTypesFromCache(PostProcessManager __instance)
    {
        //RiskOfWaitingReduxPlugin.Logger.LogMessage("Attempt ReloadBaseTypes");
        __instance.CleanBaseTypes();

        Assembly postProcessingAssembly = typeof(PostProcessEffectSettings).Assembly;
        Assembly riskOfWaitingReduxAssembly = typeof(PostProcessingCache).Assembly;
        HandleAssembly(__instance, postProcessingAssembly);
        
        string postProcessingAssemblyName = postProcessingAssembly.GetName().Name;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
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
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"Creating new cache for {assembly.FullName}");

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
                RiskOfWaitingReduxPlugin.Logger.LogMessage($"{assembly.FullName} has no cache");
                return false;
            }
            using FileStream fileStream = File.OpenRead(cachePath);
            using BinaryReader reader = new BinaryReader(fileStream);

            if (CacheHelpers.ReadAssemblyWasModified(reader, assembly))
            {
                RiskOfWaitingReduxPlugin.Logger.LogMessage($"{assembly.FullName} has an outdated cache");
                return false;
            }

            //RiskOfWaitingReduxPlugin.Logger.LogMessage($"Using cache for {assembly.FullName}");
            cachedSettingsTypes = CacheHelpers.ReadTypeCollection(reader, assembly);
        }
        catch (Exception ex)
        {
            RiskOfWaitingReduxPlugin.Logger.LogError($"PostProcessingCache for {assembly.FullName} is likely corrupted - creating new cache: {ex}");
            return false;
        }

        foreach (var settingsType in cachedSettingsTypes)
        {
            RegisterSettingsType(ppManager, settingsType, settingsType.GetCustomAttribute<PostProcessAttribute>(false));
        }
        return true;
    }
}
