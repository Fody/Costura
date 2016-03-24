using System.Globalization;
using System.IO;
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
            CreateIsolatedAssemblyCopy("ExeToProcess",
                "<Costura />",
                new string[] {
                    "AssemblyToReference.dll",
					Path.Combine("de", "AssemblyToReference.resources.dll"),
					Path.Combine("fr", "AssemblyToReference.resources.dll"),
                    "AssemblyToReferencePreEmbedded.dll",
                    "ExeToReference.exe"
                });
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");

            LoadAssemblyIntoAppDomain();
        }
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void UsingResource()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Salut", instance1.InternationalFoo());
    }
}