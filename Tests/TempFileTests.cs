using NUnit.Framework;

[TestFixture]
public class TempFileTests : BasicTests
{
    protected override string Suffix => "TempFile";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        CreateIsolatedAssemblyCopy("ExeToProcess",
            "<Costura CreateTemporaryAssemblies='true' />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"});
    }

    [SetUp]
    public void Setup()
    {
        LoadAssemblyIntoAppDomain();
    }
}