using Fody;

public class InMemoryTests : BasicTests
{
    private static readonly TestResult testResult;

    static InMemoryTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"}, "InMemory");
    }

    public override TestResult TestResult => testResult;
}
