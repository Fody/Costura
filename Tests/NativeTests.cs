using NUnit.Framework;

public abstract class NativeTests : BaseCosturaTest
{
    [Test, Category("Code")]
    public void Native()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.NativeFoo());
    }

    [Test, Category("Code")]
    public void Mixed()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFoo());
    }

    [Test, Category("Code")]
    public void MixedPInvoke()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFooPInvoke());
    }
}