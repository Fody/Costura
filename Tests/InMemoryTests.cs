using NUnit.Framework;

[TestFixture]
public class InMemoryTests : BasicTests
{
    protected override string Suffix => "InMemory";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        CreateIsolatedAssemblyCopy("ExeToProcess",
            "<Costura />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"});
    }

    [SetUp]
    public void Setup()
    {
        LoadAssemblyIntoAppDomain();
    }
}