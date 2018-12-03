using ApprovalTests;
using ApprovalTests.Namers;
using Fody;
using Xunit;
#pragma warning disable 618

public class MixedAndNativeTests : BaseCosturaTest
{
    public override TestResult TestResult => testResult;

    static TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNative.exe",
        "<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />",
        new[] {"AssemblyToReferenceMixed.dll"}, "MixedAndNative");

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

public class MixedAndNativeTestsWithEmbeddedMixed : BaseCosturaTest
{
    public override TestResult TestResult => testResult;

    static TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNativeAndEmbeddedMixed.exe",
        "<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />",
        new[] {"AssemblyToReferenceMixed.dll"}, "MixedAndNative");

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
}