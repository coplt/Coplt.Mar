using System.Runtime.ConstrainedExecution;
using System.Text;
using Coplt.Dropping;
using Coplt.Mar.Metas;
using MessagePack;

namespace Coplt.Mar;

[Dropping]
public sealed partial class MarBuilder : CriticalFinalizerObject, IAsyncDisposable
{
    #region Fields

    internal readonly Stream m_stream;
    private readonly bool m_owned;
    private bool m_closed;

    internal readonly Dictionary<string, ItemInfo> m_infos = new();

    internal bool m_entry_opened;

    #endregion

    #region Drop

    [Drop]
    private void Drop()
    {
        if (!m_closed) Close();
        if (m_owned) m_stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (!m_closed) await CloseAsync();
        if (m_owned) await m_stream.DisposeAsync();
    }

    #endregion

    #region Ctor

    private MarBuilder(Stream stream, bool owned)
    {
        m_stream = stream;
        m_owned = owned;
    }

    #endregion

    #region Flush

    public void Flush() => m_stream.Flush();

    public void FlushAsync() => m_stream.FlushAsync();

    #endregion

    #region Create

    public static MarBuilder Create(string path) =>
        Create(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read), true);

    public static ValueTask<MarBuilder> CreateAsync(string path) =>
        CreateAsync(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read), true);

    public static MarBuilder Create(Stream stream, bool owned = false)
    {
        var r = new MarBuilder(stream, owned);
        r.WriteFileHead();
        return r;
    }

    public static async ValueTask<MarBuilder> CreateAsync(Stream stream, bool owned = false)
    {
        var r = new MarBuilder(stream, owned);
        await r.WriteFileHeadAsync();
        return r;
    }

    #endregion

    #region WriteFileHead

    private void WriteFileHead() => m_stream.Write(Common.FileHead);
    private async ValueTask WriteFileHeadAsync() => await m_stream.WriteAsync(Common.FileHead);

    #endregion

    #region Close

    /// <summary>
    /// Will be auto called when Dispose
    /// </summary>
    public void Close()
    {
        if (m_entry_opened) throw new InvalidOperationException("Unable to close, the last added file has not yet been completed");
        m_closed = true;
        var offset = (ulong)m_stream.Position;
        MessagePackSerializer.Serialize(m_stream, m_infos);
        var len = (uint)((ulong)m_stream.Position - offset);
        m_stream.Write(len);
    }

    /// <summary>
    /// Will be auto called when Dispose
    /// </summary>
    public async ValueTask CloseAsync()
    {
        if (m_entry_opened) throw new InvalidOperationException("Unable to close, the last added file has not yet been completed");
        m_closed = true;
        var offset = (ulong)m_stream.Position;
        await MessagePackSerializer.SerializeAsync(m_stream, m_infos);
        var len = (uint)((ulong)m_stream.Position - offset);
        await m_stream.WriteAsync(len);
    }

    #endregion

    #region AddFile

    public FileEntry AddFile(string Name)
    {
        if (m_entry_opened) throw new InvalidOperationException("Unable to add file, the last added file has not yet been completed");
        m_entry_opened = true;
        return new FileEntry(this, (ulong)m_stream.Position, Name);
    }

    public void AddFile(string Name, Stream Data)
    {
        using var entry = AddFile(Name);
        Data.CopyTo(m_stream);
    }

    public void AddFile(string Name, ReadOnlySpan<byte> Data)
    {
        using var entry = AddFile(Name);
        m_stream.Write(Data);
    }

    public void AddFile(string Name, string Data, Encoding? encoding = null) => AddFile(Name, Data.AsSpan(), encoding);
    public void AddFile(string Name, ReadOnlySpan<char> Data, Encoding? encoding = null)
    {
        using var entry = AddFile(Name);
        using var writer = new StreamWriter(m_stream, encoding ?? Encoding.UTF8, leaveOpen: true);
        writer.Write(Data);
    }

    public FileEntryAsync AddFileAsync(string Name)
    {
        if (m_entry_opened) throw new InvalidOperationException("Unable to add file, the last added file has not yet been completed");
        m_entry_opened = true;
        return new FileEntryAsync(this, (ulong)m_stream.Position, Name);
    }

    public async ValueTask AddFileAsync(string Name, Stream Data)
    {
        await using var entry = AddFileAsync(Name);
        await Data.CopyToAsync(m_stream);
    }

    public async ValueTask AddFileAsync(string Name, ReadOnlyMemory<byte> Data)
    {
        await using var entry = AddFileAsync(Name);
        await m_stream.WriteAsync(Data);
    }

    public ValueTask AddFileAsync(string Name, string Data, Encoding? encoding = null) => AddFileAsync(Name, Data.AsMemory(), encoding);
    public async ValueTask AddFileAsync(string Name, ReadOnlyMemory<char> Data, Encoding? encoding = null)
    {
        await using var entry = AddFileAsync(Name);
        await using var writer = new StreamWriter(m_stream, encoding ?? Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(Data);
    }

    #endregion
}

public readonly struct FileEntry(MarBuilder builder, ulong Start, string Name) : IDisposable
{
    public MarBuilder Builder { get; } = builder;
    public Stream Stream => Builder.m_stream;

    public void Dispose()
    {
        Builder.m_entry_opened = false;
        var offset = (ulong)Stream.Position;
        var len = offset - Start;
        var head = new ItemMeta(len);
        MessagePackSerializer.Serialize(Stream, head);
        Builder.m_infos[Name] = new(Start, len);
        var meta_len = (byte)((ulong)Stream.Position - offset);
        Stream.WriteByte(meta_len);
    }
}

public readonly struct FileEntryAsync(MarBuilder builder, ulong Start, string Name) : IAsyncDisposable
{
    public MarBuilder Builder { get; } = builder;
    public Stream Stream => Builder.m_stream;

    public async ValueTask DisposeAsync()
    {
        Builder.m_entry_opened = false;
        var offset = (ulong)Stream.Position;
        var len = offset - Start;
        var head = new ItemMeta(len);
        await MessagePackSerializer.SerializeAsync(Stream, head);
        Builder.m_infos[Name] = new(Start, len);
        var meta_len = (byte)((ulong)Stream.Position - offset);
        Stream.WriteByte(meta_len);
    }
}
