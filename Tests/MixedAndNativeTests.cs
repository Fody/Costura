using ApprovalTests;
using ApprovalTests.Namers;
using Fody;
using Xunit;
#pragma warning disable 618

public class MixedAndNativeTests : BaseCosturaTest
{
    public override TestResult TestResult => testResult;
    static TestResult testResult;

    static MixedAndNativeTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNative.exe",
            "<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />",
            new[] {"AssemblyToReferenceMixed.dll"}, "MixedAndNative");
    }

    [Fact]
    public void Native()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance1.NativeFoo());
    }

    [Fact]
    public void Mixed()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance1.MixedFoo());
    }

    [Fact]
    public void MixedPInvoke()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance1.MixedFooPInvoke());
    }

    [Fact]
    public void TemplateHasCorrectSymbols()
    {
        using (ApprovalResults.ForScenario(nameof(MixedAndNativeTests)))
        {
            var text = Ildasm.Decompile(testResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}