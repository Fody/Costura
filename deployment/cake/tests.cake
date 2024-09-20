// Customize this file when using a different test framework
#l "tests-variables.cake"
#l "tests-nunit.cake"

public class TestProcessor : ProcessorBase
{
    public TestProcessor(BuildContext buildContext)
        : base(buildContext)
    {

    }

    public override bool HasItems()
    {
        return BuildContext.Tests.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var testProject in BuildContext.Tests.Items.ToList())
        {
            if (IgnoreTestProject(testProject))
            {
                BuildContext.Tests.Items.Remove(testProject);
            }
        }
    }

    public override async Task UpdateInfoAsync()
    {
        // Not required
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var testProject in BuildContext.Tests.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building test project '{0}'", testProject);

            var projectFileName = GetProjectFileName(BuildContext, testProject);
            
            var msBuildSettings = new MSBuildSettings
            {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, testProject, "build");

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Force disable SonarQube
            msBuildSettings.WithProperty("SonarQubeExclude", "true");

            RunMsBuild(BuildContext, testProject, projectFileName, msBuildSettings, "build");
        }
    }

    public override async Task PackageAsync()
    {
        // Not required
    }

    public override async Task DeployAsync()
    {
        // Not required
    }

    public override async Task FinalizeAsync()
    {
        // Not required
    }

    //-------------------------------------------------------------

    private bool IgnoreTestProject(string projectName)
    {
        if (BuildContext.General.IsLocalBuild && BuildContext.General.MaximizePerformance)
        {
            BuildContext.CakeContext.Information($"Local build with maximized performance detected, ignoring test project for project '{projectName}'");
            return true;
        }

        // In case of a local build and we have included / excluded anything, skip tests
        if (BuildContext.General.IsLocalBuild && 
            (BuildContext.General.Includes.Count > 0 || BuildContext.General.Excludes.Count > 0))
        {
            BuildContext.CakeContext.Information($"Skipping test project '{projectName}' because this is a local build with specific includes / excludes");
            return true;
        }

        // Special unit test part assuming a few naming conventions:
        // 1. [ProjectName].Tests
        // 2. [SolutionName].Tests.[ProjectName]
        //
        // In both cases, we can simply remove ".Tests" and check if that project is being ignored
        var expectedProjectName = projectName
            .Replace(".Integration.Tests", string.Empty)
            .Replace(".IntegrationTests", string.Empty)
            .Replace(".Tests", string.Empty);

        // Special case: if this is a "solution wide" test project, it must always run
        if (!BuildContext.RegisteredProjects.Any(x => string.Equals(x, expectedProjectName, StringComparison.OrdinalIgnoreCase)))
        {
            BuildContext.CakeContext.Information($"Including test project '{projectName}' because there are no linked projects, assuming this is a solution wide test project");
            return false;
        }

        if (!ShouldProcessProject(BuildContext, expectedProjectName))
        {
            BuildContext.CakeContext.Information($"Skipping test project '{projectName}' because project '{expectedProjectName}' should not be processed either");
            return true;
        }

        return false;
    }
}

//-------------------------------------------------------------

private static void RunUnitTests(BuildContext buildContext, string projectName)
{
    var testResultsDirectory = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, "testresults", projectName);

    buildContext.CakeContext.CreateDirectory(testResultsDirectory);

    var ranTests = false;
    var failed = false;
    var testTargetFrameworks = GetTestTargetFrameworks(buildContext, projectName);

    try
    {
        foreach (var testTargetFramework in testTargetFrameworks)
        {
            LogSeparator(buildContext.CakeContext, "Running tests for target framework {0}", testTargetFramework);
            
            if (IsDotNetCoreTargetFramework(buildContext, testTargetFramework))
            {
                buildContext.CakeContext.Information($"Project '{projectName}' is a .NET core project, using 'dotnet test' to run the unit tests");

                var projectFileName = GetProjectFileName(buildContext, projectName);

                var dotNetTestSettings = new DotNetTestSettings
                {
                    Configuration = buildContext.General.Solution.ConfigurationName,
                    // Loggers = new []
                    // {
                    //     "nunit;LogFilePath=test-result.xml"
                    // },
                    NoBuild = true,
                    NoLogo = true,
                    NoRestore = true,
                    OutputDirectory = System.IO.Path.Combine(GetProjectOutputDirectory(buildContext, projectName), testTargetFramework),
                    ResultsDirectory = testResultsDirectory
                };

                if (IsNUnitTestProject(buildContext, projectName))
                {
                    dotNetTestSettings.ArgumentCustomization = args => args
                        .Append($"-- NUnit.TestOutputXml={testResultsDirectory}");
                }
                
                if (IsXUnitTestProject(buildContext, projectName))
                {
                    var outputFileName = System.IO.Path.Combine(testResultsDirectory, $"{projectName}.xml");

                    dotNetTestSettings.ArgumentCustomization = args => args
                        .Append($"-l:trx;LogFileName={outputFileName}");
                }

                var processBit = buildContext.Tests.ProcessBit.ToLower();
                if (!string.IsNullOrWhiteSpace(processBit))
                {
                    dotNetTestSettings.Runtime = $"{buildContext.Tests.OperatingSystem}-{processBit}";
                }

                buildContext.CakeContext.Information($"Runtime: '{dotNetTestSettings.Runtime}'");

                buildContext.CakeContext.DotNetTest(projectFileName, dotNetTestSettings);

                ranTests = true;
            }
            else
            {
                buildContext.CakeContext.Information($"Project '{projectName}' is a .NET project, using '{buildContext.Tests.Framework} runner' to run the unit tests");

                if (IsNUnitTestProject(buildContext, projectName))
                {
                    RunTestsUsingNUnit(buildContext, projectName, testTargetFramework, testResultsDirectory);

                    ranTests = true;
                }
            }
        }
    }
    catch (Exception ex)
    {
        buildContext.CakeContext.Warning($"An exception occurred: {ex.Message}");

        failed = true;   
    }

    if (ranTests)
    {
        buildContext.CakeContext.Information($"Results are available in '{testResultsDirectory}'");
    }
    else if (failed)
    {
        throw new Exception("Unit test execution failed");
    }
    else
    {
        buildContext.CakeContext.Warning("No tests were executed, check whether the used test framework '{0}' is available", buildContext.Tests.Framework);
    }
}

//-------------------------------------------------------------

private static bool IsTestProject(BuildContext buildContext, string projectName)
{
    if (IsNUnitTestProject(buildContext, projectName))
    {
        return true;
    }

    if (IsXUnitTestProject(buildContext, projectName))
    {
        return true;
    }

    return false;
}

//-------------------------------------------------------------

private static bool IsNUnitTestProject(BuildContext buildContext, string projectName)
{
    var projectFileName = GetProjectFileName(buildContext, projectName);
    var projectFileContents = System.IO.File.ReadAllText(projectFileName);

    if (projectFileContents.ToLower().Contains("nunit"))
    {
        return true;
    }

    return false;

    // Not sure, return framework from config
    //return buildContext.Tests.Framework.ToLower().Equals("nunit");
}

//-------------------------------------------------------------

private static bool IsXUnitTestProject(BuildContext buildContext, string projectName)
{
    var projectFileName = GetProjectFileName(buildContext, projectName);
    var projectFileContents = System.IO.File.ReadAllText(projectFileName);

    if (projectFileContents.ToLower().Contains("xunit"))
    {
        return true;
    }

    return false;

    // Not sure, return framework from config
    //return buildContext.Tests.Framework.ToLower().Equals("xunit");
}

//-------------------------------------------------------------

private static IReadOnlyList<string> GetTestTargetFrameworks(BuildContext buildContext, string projectName)
{
    // Step 1: if defined, use defined value
    var testTargetFramework = buildContext.Tests.TargetFramework;
    if (!string.IsNullOrWhiteSpace(testTargetFramework))
    {
        buildContext.CakeContext.Information("Using test target framework '{0}', specified via the configuration", testTargetFramework);

        return new [] 
        {
            testTargetFramework
        };
    }

    buildContext.CakeContext.Information("Test target framework not specified, auto detecting test target frameworks");

    var targetFrameworks = GetTargetFrameworks(buildContext, projectName);

    buildContext.CakeContext.Information("Auto detected test target frameworks '{0}'", string.Join(", ", targetFrameworks));

    if (targetFrameworks.Length == 0)
    {
        throw new Exception(string.Format("Test target framework could not automatically be detected for project '{0]'", projectName));
    }

    return targetFrameworks;
}