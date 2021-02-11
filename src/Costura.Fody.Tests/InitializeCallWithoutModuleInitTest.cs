using Fody;

public class InitializeCallWithoutModuleInitTest : BasicTests
{
    private static readonly TestResult testResult;

    static InitializeCallWithoutModuleInitTest()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
            "<Costura LoadAtModuleInit='false' />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"},
            "InitializeCallWithoutModuleInit");
    }

    public override TestResult TestResult => testResult;
}
