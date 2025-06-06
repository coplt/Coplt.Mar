using Coplt.Mar;

namespace Test1;

class Program
{
    static async Task Main(string[] args)
    {
        {
            await using var builder = await MarBuilder.CreateAsync("./test.mar");
            await builder.AddFileAsync("test.txt", "Hello, World!");
        }

        {
            using var mar = MarArchive.Open("./test.mar");
            mar.TryGetString("test.txt", out var data);
            Console.WriteLine(data);
            if (data != "Hello, World!")
            {
                throw new Exception("Data mismatch!");
            }
        }
    }
}
