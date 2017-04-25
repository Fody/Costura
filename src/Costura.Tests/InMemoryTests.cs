using NUnit.Framework;

[TestFixture]
public class InMemoryTests : BasicTests
{
    protected override string Suffix => "InMemory";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("ExeToProcess",
                "<Costura />",
                new string[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" });
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain();
    }
}