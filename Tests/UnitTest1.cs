using Coplt.Mar;

namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup() { }

    [Test]
    public async Task Test1()
    {
        var file = $"./{nameof(Test1)}.mar";
        
        {
            await using var builder = await MarBuilder.CreateAsync(file);
            await builder.AddFileAsync("test.txt", "Hello, World!");
        }

        {
            using var mar = MarArchive.Open(file);
            mar.TryGetString("test.txt", out var data);
            Console.WriteLine(data);
            Assert.That(data, Is.EqualTo("Hello, World!"));
        }
    }
}
