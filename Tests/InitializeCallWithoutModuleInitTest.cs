using NUnit.Framework;

[TestFixture]
public class InitializeCallWithoutModuleInitTest : BasicTests
{
    protected override string Suffix => "InitializeCallWithoutModuleInit";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        CreateIsolatedAssemblyCopy("AssemblyToProcess",
            "<Costura LoadAtModuleInit='false' />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"},
            ".dll");
    }

    [SetUp]
    public void Setup()
    {
        LoadAssemblyIntoAppDomain(".dll");
    }
}