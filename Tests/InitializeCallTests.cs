using NUnit.Framework;

[TestFixture]
public class InitializeCallTests : BasicTests
{
    protected override string Suffix => "InitializeCall";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
            CreateIsolatedAssemblyCopy("AssemblyToProcess",
                "<Costura />",
                new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                ".dll");
    }

    [SetUp]
    public void Setup()
    {
            LoadAssemblyIntoAppDomain(".dll");
    }
}