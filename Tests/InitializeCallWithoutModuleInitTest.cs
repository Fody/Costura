using Fody;
using Xunit.Abstractions;

class InitializeCallWithoutModuleInitTest :
    BasicTests
    
{
    static TestResult testResult;

    static InitializeCallWithoutModuleInitTest()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
            "<Costura LoadAtModuleInit='false' />",
            new[] {"AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe"},
            "InitializeCallWithoutModuleInit");
    }

    public override TestResult TestResult => testResult;

    public InitializeCallWithoutModuleInitTest(ITestOutputHelper output) :
        base(output)
    {
    }
}