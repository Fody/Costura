#tool "nuget:?package=NUnit.ConsoleRunner&version=3.12.0"

//-------------------------------------------------------------

private static void RunTestsUsingNUnit(BuildContext buildContext, string projectName, string testTargetFramework, string testResultsDirectory)
{
    var testFile = System.IO.Path.Combine(GetProjectOutputDirectory(buildContext, projectName),
        testTargetFramework, $"{projectName}.dll");
    var resultsFile = string.Format("{0}testresults.xml", testResultsDirectory);

    var nunitSettings = new NUnit3Settings
    {
        Results = new NUnit3Result[] 
        {
            new NUnit3Result
            {
                FileName = resultsFile,
                Format = "nunit3"
            }
        },
        NoHeader = true,
        NoColor = true,
        NoResults = false,
        X86 = string.Equals(buildContext.Tests.ProcessBit, "X86", StringComparison.OrdinalIgnoreCase),
        Timeout = 60 * 1000, // 60 seconds
        Workers = 1
        //Work = testResultsDirectory
    };

    // Note: although the docs say you can use without array initialization, you can't
    buildContext.CakeContext.NUnit3(new string[] { testFile }, nunitSettings);

    buildContext.CakeContext.Information("Verifying whether results file '{0}' exists", resultsFile);

    if (!buildContext.CakeContext.FileExists(resultsFile))
    {
        throw new Exception(string.Format("Expected results file '{0}' does not exist", resultsFile));
    }
}