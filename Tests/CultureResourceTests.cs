using System.Globalization;
using System.Threading;
using ApprovalTests;
using ApprovalTests.Namers;
using Fody;
using Xunit;
#pragma warning disable 618

public class CultureResourceTests : BaseCosturaTest
{
    static TestResult testResult;

    static CultureResourceTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura />",
            new[]
            {
                "AssemblyToReference.dll",
                "de\\AssemblyToReference.resources.dll",
                "fr\\AssemblyToReference.resources.dll",
                "AssemblyToReferencePreEmbedded.dll",
                "ExeToReference.exe"
            }, "Culture");
    }

    [Fact]
    public void UsingResource()
    {
        var culture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");
            var instance1 = testResult.GetInstance("ClassToTest");
            Assert.Equal("Salut", instance1.InternationalFoo());
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }

    [Fact]
    public void TemplateHasCorrectSymbols()
    {
#if DEBUG
        var dataPoints =  "Debug";
#else
        var dataPoints = "Release";
#endif
        using (ApprovalResults.ForScenario(dataPoints))
        {
            var text = Ildasm.Decompile(TestResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }

    public override TestResult TestResult => testResult;
}