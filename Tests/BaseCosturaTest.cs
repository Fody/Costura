using System.Runtime.CompilerServices;
using Fody;
using Xunit.Abstractions;

public abstract class BaseCosturaTest :
    XunitApprovalBase
{
    public abstract TestResult TestResult { get; }

    protected BaseCosturaTest(
        ITestOutputHelper output,
        [CallerFilePath] string sourceFilePath = "") :
        base(output, sourceFilePath)
    {
    }
}