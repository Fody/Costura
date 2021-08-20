using System;
using Fody;
using NUnit.Framework;

public class MultipleNativeTests : BaseCosturaTest
{
    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy(
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
