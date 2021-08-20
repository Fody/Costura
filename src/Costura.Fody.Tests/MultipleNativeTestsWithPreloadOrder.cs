using System;
using Fody;
using NUnit.Framework;

public class MultipleNativeTestsWithPreloadOrder : BaseCosturaTest
{
    private static readonly TestResult testResult = WeavingHelper.CreateIsolatedAssemblyCopy(
        "ExeToProcessWithMultipleNative.exe",
        "<Costura PreloadOrder=\"msvcr120|msvcp120|zlib|libzstd|librdkafka|librdkafkacpp\" />", 
        Array.Empty<string>(), 
        "MultipleNativeWithPreloadOrder");

    public override TestResult TestResult => testResult;

    [Test]
    public void Native()
    {
        var instance1 = TestResult.GetInstance("ExeToProcessWithMultipleNative.Program");
        Assert.AreEqual(42, instance1.Test());
    }
}
