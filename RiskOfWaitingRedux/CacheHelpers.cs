using System.Reflection;

namespace RiskOfWaitingRedux;

public static class CacheHelpers
{
    public struct SerializableMembersLookup
    {
        public Type type;
        public List<int> membersIndices;
    }

    public static void WriteGuid(this BinaryWriter writer, Guid guid)
    {
        writer.Write(guid.ToByteArray());
    }

    public static Guid ReadGuid(this BinaryReader reader)
    {
        return new Guid(reader.ReadBytes(16));
    }

    public static bool IsLikelyMMHOOKAssembly(Assembly assembly)
    {
        return assembly.FullName.StartsWith("MMHOOK_");
    }

    public static string GetCacheDirectory(string cacheName)
    {
        return Path.Combine(Environment.CurrentDirectory, RiskOfWaitingReduxPlugin.NAME, cacheName);
    }
}
