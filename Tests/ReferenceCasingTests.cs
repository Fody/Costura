using Fody;
using Xunit.Abstractions;

public class ReferenceCasingTests : 
    BasicTests 
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

    public ReferenceCasingTests(ITestOutputHelper output) :
        base(output)
    {
    }
}
