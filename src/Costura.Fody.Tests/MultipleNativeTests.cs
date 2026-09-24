using System;
using Fody;

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
    public async Task Native()
    {
        var instance1 = TestResult.GetInstance("ExeToProcessWithMultipleNative.Program");
        await Assert.That((int)instance1.Test()).IsEqualTo(42);
    }
}
