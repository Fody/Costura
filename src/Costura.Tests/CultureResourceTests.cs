using System.Globalization;
using System.Threading;
using NUnit.Framework;

[TestFixture]
public class CultureResourceTests : BaseCosturaTest
{
    protected override string Suffix => "Culture";

    [TestFixtureSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("<Costura />");
    }

    [SetUp]
    public void Setup()
    {
        Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");

        if (AppDomainRunner.IsInTestAppDomain)
        {
            LoadAssemblyIntoAppDomain();
        }
    }
}