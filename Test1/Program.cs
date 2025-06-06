using Coplt.Mar;

namespace Test1;

class Program
{
    static async Task Main(string[] args)
    {
        {
            await using var builder = await MarBuilder.CreateAsync("./test.mar");
            await builder.AddFileAsync("test.txt", "Hello, World!");
            await builder.AddFileAsync("some.bin", new byte[] { 1, 2, 3, 4, 5 });
        }

        {
            using var mar = MarArchive.Open("./test.mar");
            
            mar.TryGetString("test.txt", out var data);
            Console.WriteLine(data);
            
            mar.TryGet("some.bin", out var span);
            Console.WriteLine(string.Join(", ", span.ToArray()));
            
            Console.WriteLine(mar.TryGet("asd", out _));
        }
    }
}
