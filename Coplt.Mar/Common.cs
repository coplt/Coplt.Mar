using System.Buffers.Binary;
using Version = Coplt.Mar.Metas.Version;

namespace Coplt.Mar;

internal static class Common
{
    public static readonly Version Version = new(0, 1, 0);
    public static readonly byte[] FileHead = "\0MAR📦"u8.ToArray();

    public static void Write(this Stream stream, uint value)
    {
        Span<byte> buf = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        stream.Write(buf);
    }

    public static async ValueTask WriteAsync(this Stream stream, uint value)
    {
        using var buf = Pooled<byte>.Rent(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        await stream.WriteAsync(buf.AsMemory(0, sizeof(uint)));
    }
}
