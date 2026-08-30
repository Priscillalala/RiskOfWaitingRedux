using BepInEx.Logging;
using HarmonyLib;
using HG.Reflection;
using RoR2;
using RoR2.ConVar;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Console = RoR2.Console;

namespace RiskOfWaitingRedux.Fixes;

// The ConVar system uses a lot of reflection to search for fields and methods which is very slow
// Vanilla actually builds a ConVar cache (ConVarNames.txt) but doesn't use it properly. RoR2BepInExPack disables it
// Fix: do the search once per assembly and save the results to a cache file
// On subsequent loads, re-use the results in the cache file, unless the assembly has been modified
public static class ConVarsCache
{
    // very few mods have convar fields, and nothing uses convar provider methods
    // writing only this byte flag saves space vs. always writing two "count" shorts which would usually be 0 
    [Flags]
    public enum ConVarCacheFlags : byte
    {
        None,
        HasConVarFields = 1,
        HasConVarProviders = 2,
    }

    private static string cacheDirectory;

    public static void Init()
    {
        cacheDirectory = CacheHelpers.GetCacheDirectory("ConVarsCache");
        Directory.CreateDirectory(cacheDirectory);
        RiskOfWaitingReduxPlugin.Harmony.PatchAll(typeof(ConVarsCache));
    }

    // FixConVar in RoR2BepInExPack is probably supposed to be hooking InitConVarsCoroutine, but it actually hooks InternalInitConVarsCoroutine
    // So, this prefix totally bypasses it
    [HarmonyPrefix, HarmonyPatch(typeof(Console), nameof(Console.InternalInitConVarsCoroutine))]
    private static bool ReplaceInternalInitConVarsCoroutine(Console __instance, ref IEnumerator __result)
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"ConVarsCache: replacing InternalInitConVarsCoroutine");

        __instance.maxPassesBeforeYielding = 100;

        InitConVarsFromCache(__instance);

        return false;
    }

    private static void InitConVarsFromCache(Console console)
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"ConVarsCache: init con vars from cache");

        List<CacheHelpers.SerializableMembers> conVarFieldsBuffer = [];
        List<CacheHelpers.SerializableMembers> conVarProvidersBuffer = [];

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetCustomAttribute<SearchableAttribute.OptInAttribute>() == null)
            {
                continue;
            }

            string cachePath = Path.Combine(cacheDirectory, assembly.FullName);

            if (!TryLoadFromCache(console, assembly, cachePath))
            {
                RegisterConVarsInAssembly(console, assembly, RiskOfWaitingReduxPlugin.Logger, conVarFieldsBuffer, conVarProvidersBuffer);
                CreateCache(assembly, cachePath, conVarFieldsBuffer, conVarProvidersBuffer);
            }

        }

        // RoR2BepInExPack fix:
        // Fix that stupid null exception when the audio manager parent volume convar are init.
        AudioManager.cvVolumeMaster.fallbackString = AudioManager.cvVolumeMaster.GetString();
    }

    private static void RegisterConVarsInAssembly(Console console, Assembly assembly, ManualLogSource logger, List<CacheHelpers.SerializableMembers> conVarFieldsResult, List<CacheHelpers.SerializableMembers> conVarProvidersResult)
    {
        conVarFieldsResult.Clear();
        conVarProvidersResult.Clear();

        foreach (var type in assembly.GetTypes())
        {
            if (TryRegisterConVarFields(console, type, logger, out var conVarFieldIndices))
            {
                conVarFieldsResult.Add(new CacheHelpers.SerializableMembers
                {
                    type = type,
                    membersIndices = conVarFieldIndices
                });
            }
            if (TryRegisterConVarProviderMethods(console, type, logger, out var conVarProviderMethodIndices))
            {
                conVarProvidersResult.Add(new CacheHelpers.SerializableMembers
                {
                    type = type,
                    membersIndices = conVarProviderMethodIndices
                });
            }
        }
    }

    private static bool TryRegisterConVarFields(Console console, Type type, ManualLogSource logger, out List<ushort> conVarFieldIndices)
    {
        bool foundAnyFields = false;
        conVarFieldIndices = null;
        try
        {
            var typeFields = GetScannableFieldsFromType(type);
            int safeFieldsCount = CacheHelpers.GetSerializableMembersCount(typeFields);
            for (ushort s = 0; s < safeFieldsCount; s++)
            {
                var field = typeFields[s];
                try
                {
                    if (field.FieldType.IsSubclassOf(typeof(BaseConVar)))
                    {
                        if (field.IsStatic)
                        {
                            RegisterConVarField(console, field);
                            if (!foundAnyFields)
                            {
                                foundAnyFields = true;
                                conVarFieldIndices = [];
                            }
                            conVarFieldIndices.Add(s);
                        }
                        else if (type.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                        {
                            RiskOfWaitingReduxPlugin.Logger.LogError($"ConVar defined as {type.Name}.{field.Name} could not be registered. " +
                                $"ConVars must be static fields.");
                        }
                    }
                }
                catch (Exception e)
                {
                    RiskOfWaitingReduxPlugin.Logger.LogError(e);
                }
            }
        }
        catch (Exception ex)
        {
            RiskOfWaitingReduxPlugin.Logger.LogError(ex);
        }
        return foundAnyFields;
    }

    private static bool TryRegisterConVarProviderMethods(Console console, Type type, ManualLogSource logger, out List<ushort> conVarProviderMethodIndices)
    {
        bool foundAnyProviderMethods = false;
        conVarProviderMethodIndices = null;
        try
        {
            var typeMethods = GetScannableMethodsFromType(type);
            int safeMethodsCount = CacheHelpers.GetSerializableMembersCount(typeMethods);
            for (ushort s = 0; s < safeMethodsCount; s++)
            {
                var method = typeMethods[s];
                try
                {
                    if (method.GetCustomAttribute<ConVarProviderAttribute>() != null)
                    {
                        if (method.ReturnType != typeof(IEnumerable<BaseConVar>) ||
                            method.GetParameters().Length != 0)
                        {
                            RiskOfWaitingReduxPlugin.Logger.LogError("ConVar provider {type.Name}.{methodInfo.Name} does not match the signature " +
                                "\"static IEnumerable<ConVar.BaseConVar>()\".");
                        }
                        else if (!method.IsStatic)
                        {
                            RiskOfWaitingReduxPlugin.Logger.LogError($"ConVar provider {type.Name}.{method.Name} could not be invoked. " +
                                $"Methods marked with the ConVarProvider attribute must be static.");
                        }
                        else
                        {
                            RegisterConVarProvider(console, method);
                            if (!foundAnyProviderMethods)
                            {
                                foundAnyProviderMethods = true;
                                conVarProviderMethodIndices = [];
                            }
                            conVarProviderMethodIndices.Add(s);
                        }
                    }
                }
                catch (Exception e)
                {
                    RiskOfWaitingReduxPlugin.Logger.LogError(e);
                }
            }
        }
        catch (Exception ex)
        {
            RiskOfWaitingReduxPlugin.Logger.LogError(ex);
        }
        return foundAnyProviderMethods;
    }

    private static FieldInfo[] GetScannableFieldsFromType(Type type)
    {
        return type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static MethodInfo[] GetScannableMethodsFromType(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static void RegisterConVarField(Console console, FieldInfo conVarField)
    {
        console.RegisterConVarInternal((BaseConVar)conVarField.GetValue(null));
    }

    private static void RegisterConVarProvider(Console console, MethodInfo conVarProvider)
    {
        foreach (BaseConVar conVar in (IEnumerable<BaseConVar>)conVarProvider.Invoke(null, Array.Empty<object>()))
        {
            console.RegisterConVarInternal(conVar);
        }
    }

    private static void CreateCache(Assembly assembly, string cachePath, List<CacheHelpers.SerializableMembers> conVarFields, List<CacheHelpers.SerializableMembers> conVarProviders)
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"Creating new cache for {assembly.FullName}");

        using FileStream fileStream = File.OpenWrite(cachePath);
        using BinaryWriter writer = new BinaryWriter(fileStream);

        CacheHelpers.WriteAssemblyIdentifer(writer, assembly);

        bool hasConVarFields = conVarFields.Count > 0;
        bool hasConVarProviders = conVarProviders.Count > 0;
        ConVarCacheFlags conVarCacheFlags = ConVarCacheFlags.None;
        if (hasConVarFields)
        {
            conVarCacheFlags |= ConVarCacheFlags.HasConVarFields;
        }
        if (hasConVarProviders)
        {
            conVarCacheFlags |= ConVarCacheFlags.HasConVarProviders;
        }
        writer.Write((byte)conVarCacheFlags);
        if (hasConVarFields)
        {
            CacheHelpers.WriteMembersCollection(writer, conVarFields);
        }
        if (hasConVarProviders)
        {
            CacheHelpers.WriteMembersCollection(writer, conVarProviders);
        }
    }

    private static bool TryLoadFromCache(Console console, Assembly assembly, string cachePath)
    {
        ICollection<FieldInfo> cachedConVarFields = null;
        ICollection<MethodInfo> cachedConVarProviders = null;

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

            RiskOfWaitingReduxPlugin.Logger.LogMessage($"Using cache for {assembly.FullName}");

            ConVarCacheFlags conVarCacheFlags = (ConVarCacheFlags)reader.ReadByte();
            if ((conVarCacheFlags & ConVarCacheFlags.HasConVarFields) > 0)
            {
                cachedConVarFields = CacheHelpers.ReadMembersCollection(reader, assembly, GetScannableFieldsFromType);
            }
            if ((conVarCacheFlags & ConVarCacheFlags.HasConVarProviders) > 0)
            {
                cachedConVarProviders = CacheHelpers.ReadMembersCollection(reader, assembly, GetScannableMethodsFromType);
            }
        }
        catch (Exception ex)
        {
            RiskOfWaitingReduxPlugin.Logger.LogError($"SearchableAttributeCache for {assembly.FullName} is likely corrupted - creating new cache: {ex}");
            return false;
        }

        if (cachedConVarFields != null)
        {
            foreach (var conVarField in cachedConVarFields)
            {
                RegisterConVarField(console, conVarField);
            }
        }
        if (cachedConVarProviders != null)
        {
            foreach (var conVarProvider in cachedConVarProviders)
            {
                RegisterConVarProvider(console, conVarProvider);
            }
        }
        return true;
    }
}
