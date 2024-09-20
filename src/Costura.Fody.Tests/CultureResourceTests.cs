using System.Globalization;
using System.Threading;
using ApprovalTests;
using ApprovalTests.Namers;
using Fody;
using NUnit.Framework;

public class CultureResourceTests : BaseCosturaTest
{
    private static readonly TestResult testResult;

    static CultureResourceTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura />",
            new[]
            {
                "AssemblyToReference.dll",
                "de\\AssemblyToReference.resources.dll",
                "fr\\AssemblyToReference.resources.dll",
                "zh-CN\\AssemblyToReference.resources.dll",
                "zh-Hans\\AssemblyToReference.resources.dll",
                "AssemblyToReferencePreEmbedded.dll",
                "ExeToReference.exe"
            }, "Culture");
    }

    [Test]
    public void Using_Resource_French()
    {
        var culture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");
            var instance1 = testResult.GetInstance("ClassToTest");
            Assert.That(instance1.InternationalFoo(), Is.EqualTo("Salut"));
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }

    [Test]
    public void Using_Resource_Chinese()
    {
        var culture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            // Yes, this seems correct, when creating the zh-Hans culture, it uses zh-CN
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("zh-Hans");
            var instance1 = testResult.GetInstance("ClassToTest");
            Assert.That(instance1.InternationalFoo(), Is.EqualTo("zh-CN"));
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }

    [Test]
    public void TemplateHasCorrectSymbols()
    {
        var dataPoints = GetScenarioName();

        using (ApprovalResults.ForScenario(dataPoints))
        {
            var text = Ildasm.Decompile(TestResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }

    public override TestResult TestResult => testResult;
}
