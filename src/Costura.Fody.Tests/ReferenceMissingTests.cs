using Fody;
using NUnit.Framework;

[TestFixture]
public class ReferenceMissingTests
{
    [Test]
    public void ThrowsForMissingReference()
    {
        // Note: this will throw WeavingException because References is null, but should actually
        // log an error about the missing assembly
        Assert.Throws<WeavingException>(() =>
        {
            WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
                "<Costura IncludeAssemblies='AssemblyToReference|AssemblyToReferencePreEmbedded|ExeToReference|MissingAssembly' />",
                new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                "InitializeCall");
        });
    }
}
