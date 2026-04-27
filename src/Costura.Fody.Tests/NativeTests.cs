using System.Threading.Tasks;
using Costura.Fody.Tests;
using NUnit.Framework;

public abstract class NativeTests : BaseCosturaTest
{
    [Test]
    public void Native()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.That("Hello", Is.EqualTo(instance1.NativeFoo()));
    }

    [Test]
    public void Mixed()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.That("Hello", Is.EqualTo(instance1.MixedFoo()));
    }

    [Test]
    public void MixedPInvoke()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        Assert.That("Hello", Is.EqualTo(instance1.MixedFooPInvoke()));
    }

    [Test]
    public async Task TemplateHasCorrectSymbols()
    {
        await VerifyHelper.AssertIlCodeAsync(TestResult.AssemblyPath);
    }
}
