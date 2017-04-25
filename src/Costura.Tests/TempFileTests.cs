using NUnit.Framework;

[TestFixture]
public class TempFileTests : BasicTests
{
    protected override string Suffix => "TempFile";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("ExeToProcess",
                "<Costura CreateTemporaryAssemblies='true' />",
                new string[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" });
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain();
    }
}