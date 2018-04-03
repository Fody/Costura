#if (DEBUG)
using ApprovalTests;
using ApprovalTests.Namers;
using NUnit.Framework;

public abstract class NativeTests : BaseCosturaTest
{
    [Test]
    public void Native()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.NativeFoo());
    }

    [Test]
    public void Mixed()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFoo());
    }

    [Test]
    public void MixedPInvoke()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFooPInvoke());
    }

    [Test]
    public void TemplateHasCorrectSymbols()
    {
        using (ApprovalResults.ForScenario(Suffix))
        {
            var text = Ildasm.Decompile(afterAssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}
#endif