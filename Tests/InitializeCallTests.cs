using Fody;
#pragma warning disable 618

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
}