using System;
using Fody;
using NUnit.Framework;

public class MultipleNativeTests : BaseCosturaTest
{
#pragma warning disable IDE1006 // Naming Styles
    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy(
#pragma warning restore IDE1006 // Naming Styles
        "ExeToProcessWithMultipleNative.exe",
        "<Costura />", 
        Array.Empty<string>(), 
        "MultipleNative");

    public override TestResult TestResult => testResult;

    [Test]
    public void Native()
    {
        var instance1 = TestResult.GetInstance("ExeToProcessWithMultipleNative.Program");
        Assert.AreEqual(42, instance1.Test());
    }
}
