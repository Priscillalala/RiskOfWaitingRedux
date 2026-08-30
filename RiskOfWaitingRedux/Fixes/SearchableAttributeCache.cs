using HarmonyLib;
using HG.Reflection;
using System.Reflection;

namespace RiskOfWaitingRedux.Fixes;

// SearchableAttribute uses a lot of reflection to search for members which is very slow
// Fix: do the search once per assembly and save the results to a cache file
// On subsequent loads, re-use the results in the cache file, unless the assembly has been modified
public static class SearchableAttributeCache
{
    private static string cacheDirectory;

    public static void Init()
    {
        cacheDirectory = CacheHelpers.GetCacheDirectory("SearchableAttributeCache");
        Directory.CreateDirectory(cacheDirectory);
        Plugin.Harmony.PatchAll(typeof(SearchableAttributeCache));
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

        string cachePath = Path.Combine(cacheDirectory, assembly.FullName);

        if (!TryLoadFromCache(assembly, cachePath))
        {
            RegisterAttributesInAssembly(assembly, out var typeTargets, out var memberTargets);
            CreateCache(assembly, cachePath, typeTargets, memberTargets);
        }

        return false;
    }

    private static SearchableAttribute[] GetSearchableAttributesOnMember(MemberInfo member)
    {
        return (SearchableAttribute[])Attribute.GetCustomAttributes(member, typeof(SearchableAttribute), false);
    }

    private static void RegisterAttributesInAssembly(Assembly assembly, out List<Type> typeTargets, out List<CacheHelpers.SerializableMembers> memberTargets)
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
                Plugin.Logger.LogDebug("ScanAssembly GetSearchableAttributesOnMember failed for :  " + type.FullName + Environment.NewLine + ex);
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
                        Plugin.Logger.LogDebug("SearchableAttribute.RegisterAttribute(attribute, type) failed for : " +
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
                Plugin.Logger.LogDebug("type.GetMembers failed for : " +
                    type.FullName +
                    Environment.NewLine +
                    ex);
            }
            int safeMembersCount = CacheHelpers.GetSerializableMembersCount(typeMembers);
            for (ushort s = 0; s < safeMembersCount; s++)
            {
                var member = typeMembers[s];
                var memberSearchableAttributes = Array.Empty<SearchableAttribute>();
                try
                {
                    memberSearchableAttributes = GetSearchableAttributesOnMember(member);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogDebug("GetSearchableAttributesOnMember failed for : " +
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
                            Plugin.Logger.LogDebug("SearchableAttribute.RegisterAttribute(attribute, memberInfo) failed for : " +
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

    private static void CreateCache(Assembly assembly, string cachePath, List<Type> typeTargets, List<CacheHelpers.SerializableMembers> memberTargets)
    {
        using FileStream fileStream = File.OpenWrite(cachePath);
        using BinaryWriter writer = new BinaryWriter(fileStream);

        CacheHelpers.WriteAssemblyIdentifer(writer, assembly);

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
                Plugin.Logger.LogError($"SearchableAttribute cache for {assembly.FullName} doesn't exist - creating new cache");
                return false;
            }
            using FileStream fileStream = File.OpenRead(cachePath);
            using BinaryReader reader = new BinaryReader(fileStream);

            if (CacheHelpers.ReadAssemblyWasModified(reader, assembly))
            {
                Plugin.Logger.LogError($"SearchableAttribute cache for {assembly.FullName} is outdated - creating new cache");
                return false;
            }

            Plugin.Logger.LogMessage($"Using cache for {assembly.FullName}");
            cachedTypeTargets = CacheHelpers.ReadTypeCollection(reader, assembly);
            cachedMemberTargets = CacheHelpers.ReadMembersCollection(reader, assembly, GetScannableMembersFromType);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"SearchableAttribute cache for {assembly.FullName} is likely corrupted - creating new cache: {ex}");
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
