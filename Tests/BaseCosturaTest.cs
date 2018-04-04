using ApprovalTests.Namers;
using Fody;
#pragma warning disable 618

public abstract class BaseCosturaTest
{
    static BaseCosturaTest()
    {
#if DEBUG
        NamerFactory.AsEnvironmentSpecificTest(() => "Debug");
#else
        NamerFactory.AsEnvironmentSpecificTest(() => "Release");
#endif
    }

    public abstract TestResult TestResult { get; }
}