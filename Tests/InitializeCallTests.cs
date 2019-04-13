using Fody;
using Xunit.Abstractions;

public class InitializeCallTests : BasicTests
{
    static TestResult testResult;

    static InitializeCallTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
            "<Costura />",
            new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
            "InitializeCall");
    }

    public override TestResult TestResult => testResult;

    public InitializeCallTests(ITestOutputHelper output) :
        base(output)
    {
    }
}