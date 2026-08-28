using BepInEx.Logging;
using HarmonyLib;
using HG.Reflection;
using System.Reflection;

namespace RiskOfWaitingRedux.Fixes;

public static class SearchableAttributeCache
{
    private static string cacheDirectory;

    public static void Init()
    {
        cacheDirectory = CacheHelpers.GetCacheDirectory("SearchableAttributeCache");
        Directory.CreateDirectory(cacheDirectory);
        RiskOfWaitingReduxPlugin.Harmony.PatchAll(typeof(SearchableAttributeCache));
    }

    [HarmonyPrefix, HarmonyPatch(typeof(SearchableAttribute), nameof(SearchableAttribute.ScanAssembly))]
    public static bool UseCacheOrScanAssembly(Assembly assembly)
    {
        if (!SearchableAttribute.assemblyBlacklist.Add(assembly.FullName))
        {
            return false;
        }
        if (assembly.GetCustomAttribute<SearchableAttribute.OptInAttribute>() == null)
        {
            return false;
        }
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"Scanning {assembly.FullName}");

        string cachePath = Path.Combine(cacheDirectory, assembly.FullName);

        if (!TryLoadFromCache(assembly, cachePath))
        {
            CreateCache(assembly, cachePath);
        }

        return false;
    }

    private static SearchableAttribute[] GetSearchableAttributesOnMember(MemberInfo member)
    {
        return (SearchableAttribute[])Attribute.GetCustomAttributes(member, typeof(SearchableAttribute), false);
    }

    private static void RegisterAttributesInAssembly(Assembly assembly, ManualLogSource logger, out List<Type> typeTargets, out List<CacheHelpers.SerializableMembers> memberTargets)
    {
        typeTargets = [];
        memberTargets = [];

        foreach (var type in assembly.GetTypes())
        {
            var typeSearchableAttributes = Array.Empty<SearchableAttribute>();
            try
            {
                typeSearchableAttributes = GetSearchableAttributesOnMember(type);
            }
            catch (Exception ex)
            {
                logger.LogDebug("ScanAssembly GetSearchableAttributesOnMember failed for :  " + type.FullName + Environment.NewLine + ex);
            }
            if (typeSearchableAttributes.Length > 0)
            {
                typeTargets.Add(type);

                foreach (var attribute in typeSearchableAttributes)
                {
                    try
                    {
                        SearchableAttribute.RegisterAttribute(attribute, type);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug("SearchableAttribute.RegisterAttribute(attribute, type) failed for : " +
                            type.FullName +
                            Environment.NewLine +
                            ex);
                    }
                }
            }
           

            List<ushort> memberIndices = null;
            bool foundAnyMembers = false;
            var typeMembers = Array.Empty<MemberInfo>();
            try
            {
                typeMembers = GetScannableMembersFromType(type);
            }
            catch (Exception ex)
            {
                logger.LogDebug("type.GetMembers failed for : " +
                    type.FullName +
                    Environment.NewLine +
                    ex);
            }
            int safeMembersLength = Math.Min(typeMembers.Length, ushort.MaxValue);
            for (ushort s = 0; s < safeMembersLength; s++)
            {
                var member = typeMembers[s];
                var memberSearchableAttributes = Array.Empty<SearchableAttribute>();
                try
                {
                    memberSearchableAttributes = GetSearchableAttributesOnMember(member);
                }
                catch (Exception ex)
                {
                    logger.LogDebug("GetSearchableAttributesOnMember failed for : " +
                        type.FullName +
                        Environment.NewLine +
                        member.Name +
                        Environment.NewLine +
                        ex);
                }
                if (memberSearchableAttributes.Length > 0)
                {
                    if (!foundAnyMembers)
                    {
                        foundAnyMembers = true;
                        memberIndices = [];
                    }
                    memberIndices.Add(s);

                    foreach (var attribute in memberSearchableAttributes)
                    {
                        try
                        {
                            SearchableAttribute.RegisterAttribute(attribute, member);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug("SearchableAttribute.RegisterAttribute(attribute, memberInfo) failed for : " +
                                type.FullName +
                                Environment.NewLine +
                                member.Name +
                                Environment.NewLine +
                                ex);
                        }
                    }
                }
            }
            if (foundAnyMembers)
            {
                memberTargets.Add(new CacheHelpers.SerializableMembers
                {
                    type = type,
                    membersIndices = memberIndices
                });
            }
        }
    }

    private static MemberInfo[] GetScannableMembersFromType(Type type)
    {
        return type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static void CreateCache(Assembly assembly, string cachePath)
    {
        RiskOfWaitingReduxPlugin.Logger.LogMessage($"Creating new cache for {assembly.FullName}");

        using FileStream fileStream = File.OpenWrite(cachePath);
        using BinaryWriter writer = new BinaryWriter(fileStream);

        CacheHelpers.WriteAssemblyIdentifer(writer, assembly);

        RegisterAttributesInAssembly(assembly, RiskOfWaitingReduxPlugin.Logger, out var typeTargets, out var memberTargets);

        CacheHelpers.WriteTypeCollection(writer, typeTargets);
        CacheHelpers.WriteMembersCollection(writer, memberTargets);
    }

    private static bool TryLoadFromCache(Assembly assembly, string cachePath)
    {
        List<(SearchableAttribute attribute, object target)> cachedSearchableAttributes = [];
        ICollection<Type> cachedTypeTargets;
        ICollection<MemberInfo> cachedMemberTargets;

        try
        {
            if (!File.Exists(cachePath))
            {
                RiskOfWaitingReduxPlugin.Logger.LogMessage($"{assembly.FullName} has no cache");
                return false;
            }
            using FileStream fileStream = File.OpenRead(cachePath);
            using BinaryReader reader = new BinaryReader(fileStream);

            if (CacheHelpers.ReadAssemblyIsOutdated(reader, assembly))
            {
                RiskOfWaitingReduxPlugin.Logger.LogMessage($"{assembly.FullName} has an outdated cache");
                return false;
            }

            RiskOfWaitingReduxPlugin.Logger.LogMessage($"Using cache for {assembly.FullName}");
            cachedTypeTargets = CacheHelpers.ReadTypeCollection(reader, assembly);
            cachedMemberTargets = CacheHelpers.ReadMembersCollection(reader, assembly, GetScannableMembersFromType);
#if false
            int typeTargetsCount = reader.ReadInt32();
            for (int i = 0; i < typeTargetsCount; i++)
            {
                string typeName = reader.ReadString();
                Type type = assembly.GetType(typeName);
                foreach (var attribute in GetSearchableAttributesOnMember(type))
                {
                    cachedSearchableAttributes.Add((attribute, type));
                }
            }
            int memberTargetsCount = reader.ReadInt32();
            for (int i = 0; i < memberTargetsCount; i++)
            {
                string typeName = reader.ReadString();
                Type type = assembly.GetType(typeName);
                var typeMembers = GetScannableMembersFromType(type);
                int memberIndicesCount = reader.ReadUInt16();
                for (int j = 0; j < memberIndicesCount; j++)
                {
                    var member = typeMembers[reader.ReadUInt16()];
                    foreach (var attribute in GetSearchableAttributesOnMember(member))
                    {
                        cachedSearchableAttributes.Add((attribute, member));
                    }
                }
            }
#endif
        }
        catch (Exception ex)
        {
            RiskOfWaitingReduxPlugin.Logger.LogError($"SearchableAttributeCache for {assembly.FullName} is likely corrupted - creating new cache: {ex}");
            return false;
        }

        foreach (var type in cachedTypeTargets)
        {
            foreach (var attribute in GetSearchableAttributesOnMember(type))
            {
                SearchableAttribute.RegisterAttribute(attribute, type);
            }
        }
        foreach (var member in cachedMemberTargets)
        {
            foreach (var attribute in GetSearchableAttributesOnMember(member))
            {
                SearchableAttribute.RegisterAttribute(attribute, member);
            }
        }
        return true;
    }
}
