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
        Assert.AreEqual("Run-OK", output);
    }

    public override TestResult TestResult => testResult;
}
