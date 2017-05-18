using NUnit.Framework;

[TestFixture]
public class Net35Tests : BasicTests
{
    protected override string Suffix => "Net35";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("ExeToProcess35",
                "<Costura />",
                new string[] { "AssemblyToReference35.dll" });
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain();
    }
}