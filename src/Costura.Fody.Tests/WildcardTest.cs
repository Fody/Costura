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

        var referencedAssemblies = wildcardWeave.Assembly.GetReferencedAssemblies().Select(_ => _.Name).ToList();
        Assert.That(referencedAssemblies, Does.Contain("AssemblyToReference"));
        Assert.That(referencedAssemblies, Does.Contain("AssemblyToReferencePreEmbedded"));

        var instance = wildcardWeave.GetInstance("ClassToTest");
        Assert.That("Hello", Is.EqualTo(instance.Simple()));
        Assert.That("Hello", Is.EqualTo(instance.SimplePreEmbed()));
    }
}
