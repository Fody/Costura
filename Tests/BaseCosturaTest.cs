using Fody;
using Xunit.Abstractions;

public abstract class BaseCosturaTest: 
    XunitApprovalBase
{
    public abstract TestResult TestResult { get; }

    protected BaseCosturaTest(ITestOutputHelper output) :
        base(output)
    {
    }
}