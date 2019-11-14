using Fody;

public class TempFileTests : BasicTests
{
    private static readonly TestResult testResult;
    public override TestResult TestResult => testResult;

    static TempFileTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura CreateTemporaryAssemblies='true' />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"}, "TempFile");
    }
}
