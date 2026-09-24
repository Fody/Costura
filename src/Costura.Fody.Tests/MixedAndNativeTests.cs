using System.Threading.Tasks;
using Fody;

public class MixedAndNativeTests : BaseCosturaTest
{
    public override TestResult TestResult => testResult;

    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNative.exe",
        "<Costura UnmanagedWinX86Assemblies='AssemblyToReferenceMixed' />",
        new[] { "AssemblyToReferenceMixed.dll" }, "MixedAndNative");

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

    [Test]
    public async Task ExecutableRunsSuccessfully()
    {
        var output = RunHelper.RunExecutable(TestResult.AssemblyPath);
        await Assert.That(output).IsEqualTo("Run-OK");
    }
}
