using Fody;

[InheritsTests]
public class InMemoryTests : BasicTests
{
    private static readonly TestResult testResult;

    static InMemoryTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"}, "InMemory");
    }

    [Test]
    public async Task ExecutableRunsSuccessfully()
    {
        var output = RunHelper.RunExecutable(TestResult.AssemblyPath);
        await Assert.That(output).IsEqualTo("Run-OK");
    }

    public override TestResult TestResult => testResult;
}
