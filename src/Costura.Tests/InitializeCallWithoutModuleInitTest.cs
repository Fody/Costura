using NUnit.Framework;

[TestFixture]
public class InitializeCallWithoutModuleInitTest : BasicTests
{
    protected override string Suffix => "InitializeCallWithoutModuleInit";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("AssemblyToProcess",
                "<Costura LoadAtModuleInit='false' />",
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