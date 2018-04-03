using ApprovalTests.Namers;
using Fody;
using NUnit.Framework;
#pragma warning disable 618

public abstract class BaseCosturaTest : BaseCostura
{
    public BaseCosturaTest()
    {
#if DEBUG
        NamerFactory.AsEnvironmentSpecificTest(() => "Debug");
#else
        NamerFactory.AsEnvironmentSpecificTest(() => "Release");
#endif
    }

    [Test]
    public void PeVerify()
    {
        PeVerifier.ThrowIfDifferent(beforeAssemblyPath, afterAssemblyPath, ignoreCodes:new []{ "0x80131869" });
    }
}