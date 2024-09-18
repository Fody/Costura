using Fody;
using NUnit.Framework;

public class MixedAndNativeTestsWithEmbeddedMixed : BaseCosturaTest
{
    public override TestResult TestResult => testResult;

#pragma warning disable IDE1006 // Naming Styles
    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcessWithNativeAndEmbeddedMixed.exe",
#pragma warning restore IDE1006 // Naming Styles
        "<Costura UnmanagedX86Assemblies='AssemblyToReferenceMixed' />",
        new[] {"AssemblyToReferenceMixed.dll"}, "MixedAndNative");

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
    public void ExecutableRunsSuccessfully()
    {
        var output = RunHelper.RunExecutable(TestResult.AssemblyPath);
        Assert.That(output, Is.EqualTo("Run-OK"));
    }
}
