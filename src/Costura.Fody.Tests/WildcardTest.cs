using System.Linq;

// weaving tests write assemblies to shared folders and load them into the same process
[NotInParallel]
public class WildcardTest
{
    [Test]
    public async Task WeavesWildcards()
    {
        var wildcardWeave = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura IncludeAssemblies=\"AssemblyToReference*\"/>",
            new[] { "AssemblyToReference.dll", "AssemblyToReferenceMixed.dll"}, "WildcardWeave");

        var referencedAssemblies = wildcardWeave.Assembly.GetReferencedAssemblies().Select(_ => _.Name).ToList();
        await Assert.That(referencedAssemblies).Contains("AssemblyToReference");
        await Assert.That(referencedAssemblies).Contains("AssemblyToReferencePreEmbedded");

        var instance = wildcardWeave.GetInstance("ClassToTest");
        await Assert.That((string)instance.Simple()).IsEqualTo("Hello");
        await Assert.That((string)instance.SimplePreEmbed()).IsEqualTo("Hello");
    }
}
