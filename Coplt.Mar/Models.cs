using MessagePack;

namespace Coplt.Mar.Metas;

[MessagePackObject(AllowPrivate = true)]
internal record struct Version([property: Key(0)] int Major, [property: Key(1)] int Minor, [property: Key(2)] int Patch);

[MessagePackObject(AllowPrivate = true)]
internal record struct ItemMeta([property: Key(0)] ulong Size);

[MessagePackObject(AllowPrivate = true)]
public record struct ItemInfo([property: Key(0)] ulong Offset, [property: Key(1)] ulong Size);
