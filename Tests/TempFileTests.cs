using Fody;
using Xunit.Abstractions;

public class TempFileTests :
    BasicTests
{
    static TestResult testResult;
    public override TestResult TestResult => testResult;

    static TempFileTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura CreateTemporaryAssemblies='true' />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"}, "TempFile");
    }

    public TempFileTests(ITestOutputHelper output) : 
        base(output)
    {
    }
}