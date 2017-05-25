using NUnit.Framework;

[TestFixture]
public class NoInitializeTest : BaseCostura
{
    protected override string Suffix => "NoInitialize";

    [Test]
    public void FailsToWeave()
    {
        Assert.Throws<WeavingException>(() =>
            CreateIsolatedAssemblyCopy("AssemblyWithoutInitialize",
                "<Costura LoadAtModuleInit='false' />",
                new string[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                ".dll"),
            "Costura was not initialized. Make sure LoadAtModuleInit=true or call CosturaUtility.Initialize().");
    }
}