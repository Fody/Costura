using ApprovalTests;
using ApprovalTests.Namers;
using Xunit;

public abstract class NativeTests : BaseCosturaTest
{
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
        using (ApprovalResults.ForScenario(nameof(NativeTests)))
        {
            var text = Ildasm.Decompile(TestResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}