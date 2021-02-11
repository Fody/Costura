using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class Integration
{
    [Test, Explicit]
    public void Test()
    {
        // just use some code from Newtonsoft.Json to ensure it's referenced.
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(this);
        Assert.AreEqual("{\"SomeProperty\":\"Test\"}", json);

        // just use some code to ensure assemblies are properly embedded.
        var now = Catel.FastDateTime.Now;

        Console.WriteLine(now);

        var embeddedAssembly = typeof(Catel.FastDateTime).Assembly;
        var thisAssembly = GetType().Assembly;

        Assert.IsEmpty(embeddedAssembly.Location);
        // does not work on build server: Assert.Equal(embeddedAssembly.CodeBase, thisAssembly.CodeBase, StringComparer.OrdinalIgnoreCase);

        var targetDir = Path.GetDirectoryName(new Uri(thisAssembly.CodeBase).LocalPath);

        var localCopyFiles = Directory.EnumerateFiles(targetDir)
            .Select(Path.GetFileName)
            .ToArray();

        Assert.IsTrue(localCopyFiles.Any(file => file.StartsWith("Newtonsoft", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(localCopyFiles.Any(file => file.StartsWith("Catel.Core", StringComparison.OrdinalIgnoreCase)));
    }

    public string SomeProperty { get; set; } = "Test";
}
