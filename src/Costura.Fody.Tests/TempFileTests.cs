using Fody;

[InheritsTests]
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

    [Test]
#if NETCORE
    // Somehow this only succeeds when ran manually for .NET Core
    [Explicit]
#endif
    public async Task ExecutableRunsSuccessfully()
    {
        var output = RunHelper.RunExecutable(TestResult.AssemblyPath);
        await Assert.That(output).IsEqualTo("Run-OK");
    }
}
