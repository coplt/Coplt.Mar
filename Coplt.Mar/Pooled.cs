using System.Buffers;

namespace Coplt.Mar;

internal readonly record struct Pooled<T>(T[] Array) : IDisposable
{
    public static Pooled<T> Rent(int Size) => new(ArrayPool<T>.Shared.Rent(Size));

    public void Dispose() => ArrayPool<T>.Shared.Return(Array);

    public Span<T> Span => Array;
    public Memory<T> Memory => Array;

    public Span<T> AsSpan(int start, int count) => Array.AsSpan(start, count);
    public Memory<T> AsMemory(int start, int count) => Array.AsMemory(start, count);

    public static implicit operator Span<T>(Pooled<T> pooled) => pooled.Array;
    public static implicit operator ReadOnlySpan<T>(Pooled<T> pooled) => pooled.Array;
}
