using System.Linq;
using Xunit;

public class WildcardTest
{
    [Fact]
    public void WeavesWildcards()
    {
        var wildcardWeave = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura IncludeAssemblies=\"AssemblyToReference*\"/>",
            new[] { "AssemblyToReference.dll", "AssemblyToReferenceMixed.dll"}, "WildcardWeave");

        var referencedAssemblies = wildcardWeave.Assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
        Assert.Contains("AssemblyToReference", referencedAssemblies);
        Assert.Contains("AssemblyToReferencePreEmbedded", referencedAssemblies);

        var instance = wildcardWeave.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance.Simple());
        Assert.Equal("Hello", instance.SimplePreEmbed());
    }
}