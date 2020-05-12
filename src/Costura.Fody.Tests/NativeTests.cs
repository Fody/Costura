using ApprovalTests;
using ApprovalTests.Namers;
using NUnit.Framework;

public abstract class NativeTests : BaseCosturaTest
{
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
        var dataPoints = GetScenarioName();

        using (ApprovalResults.ForScenario(dataPoints))
        {
            var text = Ildasm.Decompile(TestResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}
