# Coplt.Mar

[![Nuget](https://img.shields.io/nuget/v/Coplt.Mar)](https://www.nuget.org/packages/Coplt.Mar/)

**M**emory mapped **AR**chiver format

- Random Access
- No compression
- No hash verification (CRC SHA ...)
- Cannot be modified after creation
- Get `ReadOnlySpan<byte>` directly through memory mapping

### Detail

File layout:

```
<Head> <Version> <Data> <Data> ... <Manifest> <Tail>
```

- Head

  `"\0MAR📦"` in utf8, 📦 is emoji  
  `00 4D 41 52 F0 9F 93 A6`

- Version

  `(int Major, int Minor, int Patch)` ser by MsgPack  
  It is the format version, not the library version  
  `0.1.0` is `93 00 01 00`

- Data

  Just simply bytes

- Manifest

  `Dictionary<string, (ulong Offset, ulong Size)>` ser by MsgPack

- Tail

  Little endian `uint` manifest size

## Usage

```csharp
{
    await using var builder = await MarBuilder.CreateAsync("./test.mar");
    await builder.AddFileAsync("test.txt", "Hello, World!");
    // await builder.AddFileAsync("test.txt", "Hello, World!", Encoding.Utf16); // Specify encoding
    await builder.AddFileAsync("some.bin", new byte[] { 1, 2, 3, 4, 5 });
    // await builder.AddFileAsync("test.txt", SomeStream);
}

{
    using var mar = MarArchive.Open("./test.mar");
    
    mar.TryGetString("test.txt", out string data);
    Console.WriteLine(data);
    // Output: Hello, World!
    
    mar.TryGet("test.txt", out ReadOnlySpan<byte> span);
    Console.WriteLine(string.Join(", ", span.ToArray()));
    // Output: 1, 2, 3, 4, 5
    
    Console.WriteLine(mar.TryGet("asd", out _));
    // Output: False
}
```
