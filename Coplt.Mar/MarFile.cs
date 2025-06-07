using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;
using Coplt.Dropping;
using Coplt.Mar.Metas;
using MessagePack;
using Microsoft.Win32.SafeHandles;

namespace Coplt.Mar;

[Dropping(Unmanaged = true)]
public sealed partial class MarFile
{
    #region Fields

    private readonly SafeFileHandle m_handle;
    private readonly long m_size;
    private readonly FrozenDictionary<string, ItemInfo> m_info;
    private readonly bool m_owned;

    #endregion

    #region Properties

    public SafeFileHandle RawHandle => m_handle;
    public long RawSize => m_size;
    public FrozenDictionary<string, ItemInfo> Manifest => m_info;

    #endregion

    #region Drop

    [Drop]
    private void Drop()
    {
        if (m_owned) m_handle.Dispose();
    }

    #endregion

    #region Ctor

    private MarFile(SafeFileHandle handle, bool owned, long size, FrozenDictionary<string, ItemInfo> info)
    {
        m_handle = handle;
        m_owned = owned;
        m_size = size;
        m_info = info;
    }

    #endregion

    #region Open

    public static MarFile Open(SafeFileHandle handle, bool owned)
    {
        long size;
        FrozenDictionary<string, ItemInfo> info;
        try
        {
            size = RandomAccess.GetLength(handle);
            {
                if (size < Common.FileHead.Length + sizeof(uint)) throw new NotSupportedException("This file is not a Mar archive file");
                Span<byte> head = stackalloc byte[Common.FileHead.Length];
                var r = RandomAccess.Read(handle, head, 0);
                if (r != Common.FileHead.Length) throw new NotSupportedException("This file is not a Mar archive file");
                if (!head.SequenceEqual(Common.FileHead)) throw new NotSupportedException("This file is not a Mar archive file");
            }

            {
                var manifest_offset = size - sizeof(uint);
                Span<byte> buf = stackalloc byte[sizeof(uint)];
                var r = RandomAccess.Read(handle, buf, manifest_offset);
                if (r != sizeof(uint)) throw new NotSupportedException("This file is not a Mar archive file");
                var manifest_size = (int)BinaryPrimitives.ReadUInt32LittleEndian(buf);
                using var mem = Pooled<byte>.Rent(manifest_size);
                r = RandomAccess.Read(handle, mem.Span[..manifest_size], manifest_offset - manifest_size);
                if (r != manifest_size) throw new IOException();
                info = MessagePackSerializer.Deserialize<FrozenDictionary<string, ItemInfo>>(mem.AsMemory(0, manifest_size));
            }
        }
        catch
        {
            if (owned) handle.Dispose();
            throw;
        }
        return new(handle, owned, size, info);
    }

    public static MarFile Open(string path) =>
        Open(File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.RandomAccess), true);

    public static async ValueTask<MarFile> OpenAsync(SafeFileHandle handle, bool owned)
    {
        long size;
        FrozenDictionary<string, ItemInfo> info;
        try
        {
            size = RandomAccess.GetLength(handle);
            using var buffer = Pooled<byte>.Rent(1024);

            {
                if (size < Common.FileHead.Length + sizeof(uint)) throw new NotSupportedException("This file is not a Mar archive file");
                var r = await RandomAccess.ReadAsync(handle, buffer.Memory[..Common.FileHead.Length], 0);
                if (r != Common.FileHead.Length) throw new NotSupportedException("This file is not a Mar archive file");
                if (!buffer.Span[..Common.FileHead.Length].SequenceEqual(Common.FileHead))
                    throw new NotSupportedException("This file is not a Mar archive file");
            }

            {
                var manifest_offset = size - sizeof(uint);
                var r = await RandomAccess.ReadAsync(handle, buffer.Memory[..sizeof(uint)], manifest_offset);
                if (r != sizeof(uint)) throw new NotSupportedException("This file is not a Mar archive file");
                var manifest_size = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span[..sizeof(uint)]);
                if (manifest_size < 1024)
                {
                    r = await RandomAccess.ReadAsync(handle, buffer.Memory[..manifest_size], manifest_offset - manifest_size);
                    if (r != manifest_size) throw new IOException();
                    info = MessagePackSerializer.Deserialize<FrozenDictionary<string, ItemInfo>>(buffer.Memory[..manifest_size]);
                }
                else
                {
                    using var mem = Pooled<byte>.Rent(manifest_size);
                    r = await RandomAccess.ReadAsync(handle, mem.Memory[..manifest_size], manifest_offset - manifest_size);
                    if (r != manifest_size) throw new IOException();
                    info = MessagePackSerializer.Deserialize<FrozenDictionary<string, ItemInfo>>(mem.Memory[..manifest_size]);
                }
            }
        }
        catch
        {
            if (owned) handle.Dispose();
            throw;
        }
        return new(handle, owned, size, info);
    }

    public static ValueTask<MarFile> OpenAsync(string path) =>
        OpenAsync(File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.RandomAccess), true);

    #endregion

    #region Count

    public int Count => m_info.Count;

    #endregion

    #region Contains

    public bool Contains(string name) => m_info.ContainsKey(name);

    #endregion

    #region TryGet

    public bool TryGetInfo(string name, out ItemInfo info) => m_info.TryGetValue(name, out info);

    #endregion

    #region TryRead

    public bool TryRead(string name, Span<byte> buffer)
    {
        if (!m_info.TryGetValue(name, out var info)) return false;
        if (info.Size > (uint)buffer.Length) return false;
        var r = RandomAccess.Read(m_handle, buffer[..(int)info.Size], (long)info.Offset);
        if ((uint)r != info.Size) throw new IOException();
        return true;
    }

    public async ValueTask<bool> TryReadAsync(string name, Memory<byte> buffer, CancellationToken cancel = default)
    {
        if (!m_info.TryGetValue(name, out var info)) return false;
        if (info.Size > (uint)buffer.Length) return false;
        var r = await RandomAccess.ReadAsync(m_handle, buffer[..(int)info.Size], (long)info.Offset, cancel);
        if ((uint)r != info.Size) throw new IOException();
        return true;
    }

    public byte[]? TryRead(string name)
    {
        if (!m_info.TryGetValue(name, out var info)) return null;
        var data = new byte[info.Size];
        var r = RandomAccess.Read(m_handle, data, (long)info.Offset);
        if ((uint)r != info.Size) throw new IOException();
        return data;
    }

    public async ValueTask<byte[]?> TryReadAsync(string name, CancellationToken cancel = default)
    {
        if (!m_info.TryGetValue(name, out var info)) return null;
        var data = new byte[info.Size];
        var r = await RandomAccess.ReadAsync(m_handle, data, (long)info.Offset, cancel);
        if ((uint)r != info.Size) throw new IOException();
        return data;
    }

    public string? TryReadString(string name, Encoding? encoding = null)
    {
        if (!m_info.TryGetValue(name, out var info)) return null;
        using var buffer = Pooled<byte>.Rent((int)info.Size);
        var r = RandomAccess.Read(m_handle, buffer.Span[..(int)info.Size], (long)info.Offset);
        if ((uint)r != info.Size) throw new IOException();
        return (encoding ?? Encoding.UTF8).GetString(buffer.Span[..(int)info.Size]);
    }

    public async ValueTask<string?> TryReadStringAsync(string name, Encoding? encoding = null, CancellationToken cancel = default)
    {
        if (!m_info.TryGetValue(name, out var info)) return null;
        using var buffer = Pooled<byte>.Rent((int)info.Size);
        var r = await RandomAccess.ReadAsync(m_handle, buffer.Memory[..(int)info.Size], (long)info.Offset, cancel);
        if ((uint)r != info.Size) throw new IOException();
        return (encoding ?? Encoding.UTF8).GetString(buffer.Span[..(int)info.Size]);
    }

    #endregion
}
