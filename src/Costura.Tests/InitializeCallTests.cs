using NUnit.Framework;

[TestFixture]
public class InitializeCallTests : BasicTests
{
    protected override string Suffix => "InitializeCall";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("AssemblyToProcess",
                "<Costura />",
                new string[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
                ".dll");
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain(".dll");
    }
}