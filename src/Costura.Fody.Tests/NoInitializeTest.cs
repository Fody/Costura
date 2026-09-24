using System;
using Fody;

// weaving tests write assemblies to shared folders and load them into the same process
[NotInParallel]
public class NoInitializeTest
{
    [Test]
    public async Task FailsToWeave()
    {
        await Assert.That(new Action(() =>
                WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyWithoutInitialize.dll",
                "<Costura LoadAtModuleInit='false' />",
                new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                    "NoInitialize"))).Throws<WeavingException>();
    }
}
