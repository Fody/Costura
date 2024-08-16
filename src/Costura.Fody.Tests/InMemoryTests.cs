using Fody;
using NUnit.Framework;

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
    public void ExecutableRunsSuccessfully()
    {
        var output = RunHelper.RunExecutable(TestResult.AssemblyPath);
        Assert.That(output, Is.EqualTo("Run-OK"));
    }

    public override TestResult TestResult => testResult;
}
