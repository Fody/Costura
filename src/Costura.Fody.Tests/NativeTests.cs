using System.Threading.Tasks;
using Costura.Fody.Tests;

public abstract class NativeTests : BaseCosturaTest
{
    [Test]
    public async Task Native()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        await Assert.That((string)instance1.NativeFoo()).IsEqualTo("Hello");
    }

    [Test]
    public async Task Mixed()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        await Assert.That((string)instance1.MixedFoo()).IsEqualTo("Hello");
    }

    [Test]
    public async Task MixedPInvoke()
    {
        var instance1 = TestResult.GetInstance("ClassToTest");
        await Assert.That((string)instance1.MixedFooPInvoke()).IsEqualTo("Hello");
    }

    [Test]
    public async Task TemplateHasCorrectSymbols()
    {
        await VerifyHelper.AssertIlCodeAsync(TestResult.AssemblyPath);
    }
}
