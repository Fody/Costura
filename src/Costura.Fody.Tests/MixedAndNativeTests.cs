using ApprovalTests;
using ApprovalTests.Namers;
using Fody;
using NUnit.Framework;

public class MixedAndNativeTests : BaseCosturaTest
{
    public override TestResult TestResult => testResult;

    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNative.exe",
        "<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />",
        new[] {"AssemblyToReferenceMixed.dll"}, "MixedAndNative");

    [Test]
    public void Native()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.NativeFoo());
    }

    [Test]
    public void Mixed()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFoo());
    }

    [Test]
    public void MixedPInvoke()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFooPInvoke());
    }

    [Test]
    public void TemplateHasCorrectSymbols()
    {
#if DEBUG
        var dataPoints = "Debug";
#else
        var dataPoints = "Release";
#endif
        using (ApprovalResults.ForScenario(dataPoints))
        {
            var text = Ildasm.Decompile(testResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}
