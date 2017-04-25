using NUnit.Framework;

[TestFixture]
public class MixedAndNativeTests : BaseCosturaTest
{
    protected override string Suffix => "MixedAndNative";

    [OneTimeSetUp]
    public void CreateAssembly()
    {
        if (AppDomainRunner.IsNotInTestAppDomain)
            CreateIsolatedAssemblyCopy("ExeToProcessWithNative",
                "<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />",
                new string[] { "AssemblyToReferenceMixed.dll" });
    }

    [SetUp]
    public void Setup()
    {
        if (AppDomainRunner.IsInTestAppDomain)
            LoadAssemblyIntoAppDomain();
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void Native()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.NativeFoo());
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void Mixed()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFoo());
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void MixedPInvoke()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFooPInvoke());
    }
}