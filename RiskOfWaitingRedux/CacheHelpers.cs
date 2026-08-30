using System.Reflection;

namespace RiskOfWaitingRedux;

public static class CacheHelpers
{
    public struct SerializableMembers
    {
        public readonly ushort MemberIndicesCount => (ushort)membersIndices.Count;

        public Type type;
        public List<ushort> membersIndices;
    }

    public static void Write(this BinaryWriter writer, Guid guid)
    {
        writer.Write(guid.ToByteArray());
    }

    public static Guid ReadGuid(this BinaryReader reader)
    {
        return new Guid(reader.ReadBytes(16));
    }

    public static void WriteAssemblyIdentifer(BinaryWriter writer, Assembly assembly)
    {
        writer.Write(assembly.ManifestModule.ModuleVersionId);
    }

    public static bool ReadAssemblyWasModified(BinaryReader reader, Assembly assembly)
    {
        Guid cachedVersionId = reader.ReadGuid();
        return assembly.ManifestModule.ModuleVersionId != cachedVersionId;
    }

    public static void WriteTypeCollection(BinaryWriter writer, ICollection<Type> typeCollection)
    {
        writer.Write(typeCollection.Count);
        foreach (Type type in typeCollection)
        {
            writer.Write(type.FullName);
        }
    }

    public static ICollection<Type> ReadTypeCollection(BinaryReader reader, Assembly assembly)
    {
        int typeCollectionCount = reader.ReadInt32();
        Type[] result = new Type[typeCollectionCount];
        for (int i = 0; i < typeCollectionCount; i++)
        {
            string typeName = reader.ReadString();
            result[i] = assembly.GetType(typeName, true);
        }
        return result;
    }

    public static void WriteMembersCollection(BinaryWriter writer, ICollection<SerializableMembers> membersCollection)
    {
        writer.Write(membersCollection.Count);
        foreach (SerializableMembers members in membersCollection)
        {
            writer.Write(members.type.FullName);
            writer.Write(members.MemberIndicesCount);
            foreach (ushort memberIndex in members.membersIndices)
            {
                writer.Write(memberIndex);
            }
        }
    }

    public static ICollection<TMember> ReadMembersCollection<TMember>(BinaryReader reader, Assembly assembly, Func<Type, TMember[]> getTypeMembers)
    {
        List<TMember> result = [];
        int memberTargetsCount = reader.ReadInt32();
        for (int i = 0; i < memberTargetsCount; i++)
        {
            string typeName = reader.ReadString();
            Type type = assembly.GetType(typeName, true);
            var typeMembers = getTypeMembers(type);
            int memberIndicesCount = reader.ReadUInt16();
            for (int j = 0; j < memberIndicesCount; j++)
            {
                result.Add(typeMembers[reader.ReadUInt16()]);
            }
        }
        return result;
    }

    public static bool IsLikelyMMHOOKAssembly(Assembly assembly)
    {
        return assembly.FullName.StartsWith("MMHOOK_");
    }

    public static string GetCacheDirectory(string cacheName)
    {
        return Path.Combine(RiskOfWaitingReduxPlugin.DataDirectory, cacheName);
    }

    public static int GetSerializableMembersCount(Array members)
    {
        return Math.Min(members.Length, ushort.MaxValue);
    }
}
