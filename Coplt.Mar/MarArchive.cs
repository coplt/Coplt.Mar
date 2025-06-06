using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Text;
using Coplt.Dropping;
using Coplt.Mar.Metas;
using MessagePack;
using Microsoft.Win32.SafeHandles;

namespace Coplt.Mar;

[Dropping(Unmanaged = true)]
public sealed unsafe partial class MarArchive
{
    #region Fields

    private readonly SafeFileHandle m_handle;
    private readonly MemoryMappedFile m_file;
    private readonly MemoryMappedViewAccessor m_access;
    private readonly MemoryMappedViewStream m_stream;
    private readonly byte* m_ptr;
    private readonly long m_size;
    private readonly FrozenDictionary<string, ItemInfo> m_info;
    private readonly bool m_owned;

    #endregion

    #region Properties

    public MemoryMappedFile RawMemoryMappedFile => m_file;
    public byte* RawPointer => m_ptr;
    public long RawSize => m_size;
    public FrozenDictionary<string, ItemInfo> Manifest => m_info;

    #endregion

    #region Drop

    [Drop]
    private void Drop()
    {
        m_access.SafeMemoryMappedViewHandle.ReleasePointer();
        m_access.Dispose();
        m_stream.Dispose();
        m_file.Dispose();
        if (m_owned) m_handle.Dispose();
    }

    #endregion

    #region Ctor

    private MarArchive(SafeFileHandle handle, bool owned)
    {
        m_handle = handle;
        m_owned = owned;
        m_file = MemoryMappedFile.CreateFromFile(handle, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
        m_size = RandomAccess.GetLength(handle);
        m_access = m_file.CreateViewAccessor(0, m_size, MemoryMappedFileAccess.Read);
        m_stream = m_file.CreateViewStream(0, m_size, MemoryMappedFileAccess.Read);
        m_access.SafeMemoryMappedViewHandle.AcquirePointer(ref m_ptr);

        var head = GetSpan(0, Common.FileHead.Length);
        if (!head.SequenceEqual(Common.FileHead)) throw new NotSupportedException("This file is not a Mar archive file");
        var manifest_offset = m_size - sizeof(uint);
        var manifest_size = BinaryPrimitives.ReadUInt32LittleEndian(GetSpan(manifest_offset, sizeof(uint)));
        m_stream.Position = manifest_offset - manifest_size;
        m_info = MessagePackSerializer.Deserialize<FrozenDictionary<string, ItemInfo>>(m_stream);
    }

    #endregion

    #region Open

    public static MarArchive Open(SafeFileHandle file, bool owned) => new(file, owned);

    public static MarArchive Open(string path) =>
        new(File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.RandomAccess), true);

    #endregion

    #region GetSpan

    public ReadOnlySpan<byte> GetSpan(long offset, int len) => new(m_ptr + offset, len);
    public ReadOnlySpan<byte> GetSpan(ulong offset, int len) => new(m_ptr + offset, len);

    #endregion

    #region Count

    public int Count => m_info.Count;

    #endregion

    #region Contains

    public bool Contains(string name) => m_info.ContainsKey(name);

    #endregion

    #region TryGet

    public bool TryGetInfo(string name, out ItemInfo info) => m_info.TryGetValue(name, out info);

    public bool TryGet(string name, out ReadOnlySpan<byte> data)
    {
        if (!m_info.TryGetValue(name, out var info))
        {
            data = default;
            return false;
        }
        data = GetSpan(info.Offset, (int)info.Size);
        return true;
    }

    public bool TryGetString(string name, [NotNullWhen(true)] out string? data, Encoding? encoding = null)
    {
        if (!TryGet(name, out var span))
        {
            data = null;
            return false;
        }
        data = (encoding ?? Encoding.UTF8).GetString(span);
        return true;
    }

    #endregion
}
