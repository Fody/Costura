using System.Linq;
using NUnit.Framework;

public class WildcardTest
{
    [Test]
    public void WeavesWildcards()
    {
        var wildcardWeave = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura IncludeAssemblies=\"AssemblyToReference*\"/>",
            new[] { "AssemblyToReference.dll", "AssemblyToReferenceMixed.dll"}, "WildcardWeave");

        var referencedAssemblies = wildcardWeave.Assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
        Assert.Contains("AssemblyToReference", referencedAssemblies);
        Assert.Contains("AssemblyToReferencePreEmbedded", referencedAssemblies);

        var instance = wildcardWeave.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance.Simple());
        Assert.AreEqual("Hello", instance.SimplePreEmbed());
    }
}
