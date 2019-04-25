#pragma warning disable 618

using Fody;

public class ReferenceCasingTests : BasicTests
{
    private static readonly TestResult testResult;

    static ReferenceCasingTests()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
            "<Costura IncludeAssemblies='assemblytoreference|exetoreference' />",
            new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
            "InitializeCall");
    }

    public override TestResult TestResult => testResult;
}
