using System;
using Fody;

// weaving tests write assemblies to shared folders and load them into the same process
[NotInParallel]
public class ReferenceMissingTests
{
    [Test]
    public async Task ThrowsForMissingReference()
    {
        // Note: this will throw WeavingException because References is null, but should actually
        // log an error about the missing assembly
        await Assert.That(new Action(() =>
        {
            WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
                "<Costura IncludeAssemblies='AssemblyToReference|AssemblyToReferencePreEmbedded|ExeToReference|MissingAssembly' />",
                new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                "InitializeCall");
        })).Throws<WeavingException>();
    }
}
