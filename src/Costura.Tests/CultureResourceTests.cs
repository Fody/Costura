using System.Globalization;
using System.Threading;
using NUnit.Framework;

[TestFixture]
public class CultureResourceTests : BaseCosturaTest
{
    protected override string Suffix => "Culture";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("ExeToProcess",
                "<Costura />",
                new string[] {
                    "AssemblyToReference.dll",
                    "de\\AssemblyToReference.resources.dll",
                    "fr\\AssemblyToReference.resources.dll",
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