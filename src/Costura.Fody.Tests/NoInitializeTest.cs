using Fody;
using NUnit.Framework;

[TestFixture]
public class NoInitializeTest
{
    [Test]
    public void FailsToWeave()
    {
        Assert.Throws<WeavingException>(() =>
                WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyWithoutInitialize.dll",
                "<Costura LoadAtModuleInit='false' />",
                new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                    "NoInitialize"));
    }
}
