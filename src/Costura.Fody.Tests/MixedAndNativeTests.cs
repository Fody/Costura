using System.Threading.Tasks;
using Fody;
using NUnit.Framework;

public class MixedAndNativeTests : BaseCosturaTest
{
    public override TestResult TestResult => testResult;

    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNative.exe",
        "<Costura UnmanagedWinX86Assemblies='AssemblyToReferenceMixed' />",
        new[] { "AssemblyToReferenceMixed.dll" }, "MixedAndNative");

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

    [Test]
    public void ExecutableRunsSuccessfully()
    {
        var output = RunHelper.RunExecutable(TestResult.AssemblyPath);
        Assert.That(output, Is.EqualTo("Run-OK"));
    }
}
