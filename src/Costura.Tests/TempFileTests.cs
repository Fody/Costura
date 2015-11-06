using NUnit.Framework;

[TestFixture]
public class TempFileTests : BaseCosturaTest
{
    protected override string Suffix => "TempFile";

    [TestFixtureSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("<Costura CreateTemporaryAssemblies='true' />");
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain();
    }
}