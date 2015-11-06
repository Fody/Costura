using NUnit.Framework;

[TestFixture]
public class InMemoryTests : BaseCosturaTest
{
    protected override string Suffix => "InMemory";

    [TestFixtureSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("<Costura />");
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain();
    }
}