using Fody;
#pragma warning disable 618

public class InMemoryTests : BasicTests
{
    static TestResult testResult;

    static InMemoryTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("ExeToProcess.exe",
            "<Costura />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"}, "InMemory");
    }

    public override TestResult TestResult => testResult;
}