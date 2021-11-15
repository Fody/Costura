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

            ConfigureMsBuild(BuildContext, msBuildSettings, testProject);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Force disable SonarQube
            msBuildSettings.WithProperty("SonarQubeExclude", "true");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(BuildContext, testProject);
            BuildContext.CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            RunMsBuild(BuildContext, testProject, projectFileName, msBuildSettings);
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
        var expectedProjectName = projectName.Replace(".Tests", string.Empty);
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
    var testResultsDirectory = System.IO.Path.Combine(buildContext.General.OutputRootDirectory,
        "testresults", projectName);

    buildContext.CakeContext.CreateDirectory(testResultsDirectory);

    var ranTests = false;
    var failed = false;
    var testTargetFramework = GetTestTargetFramework(buildContext, projectName);

    try
    {
        if (IsDotNetCoreProject(buildContext, projectName))
        {
            buildContext.CakeContext.Information("Project '{0}' is a .NET core project, using 'dotnet test' to run the unit tests", projectName);

            var projectFileName = GetProjectFileName(buildContext, projectName);

            var dotNetCoreTestSettings = new DotNetCoreTestSettings
            {
                Configuration = buildContext.General.Solution.ConfigurationName,
                NoBuild = true,
                NoLogo = true,
                NoRestore = true,
                OutputDirectory = System.IO.Path.Combine(GetProjectOutputDirectory(buildContext, projectName), testTargetFramework),
                ResultsDirectory = testResultsDirectory
            };

            var processBit = buildContext.Tests.ProcessBit.ToLower();
            if (!string.IsNullOrWhiteSpace(processBit))
            {
                dotNetCoreTestSettings.Runtime = $"win-{processBit}";
            }

            buildContext.CakeContext.DotNetCoreTest(projectFileName, dotNetCoreTestSettings);

            ranTests = true;
        }
        else
        {
            buildContext.CakeContext.Information("Project '{0}' is a .NET project, using '{1} runner' to run the unit tests", projectName, buildContext.Tests.Framework);

            if (buildContext.Tests.Framework.ToLower().Equals("nunit"))
            {
                RunTestsUsingNUnit(buildContext, projectName, testTargetFramework, testResultsDirectory);

                ranTests = true;
            }
        }
    }
    catch (Exception ex)
    {
        buildContext.CakeContext.Warning("An exception occurred: {0}", ex.Message);

        failed = true;   
    }

    if (ranTests)
    {
        buildContext.CakeContext.Information("Results are available in '{0}'", testResultsDirectory);
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

private static string GetTestTargetFramework(BuildContext buildContext, string projectName)
{
    // Step 1: if defined, use defined value
    var testTargetFramework = buildContext.Tests.TargetFramework;
    if (!string.IsNullOrWhiteSpace(testTargetFramework))
    {
        buildContext.CakeContext.Information("Using test target framework '{0}', specified via the configuration", testTargetFramework);

        return testTargetFramework;
    }

    buildContext.CakeContext.Information("Test target framework not specified, auto detecting test target framework");

    var targetFrameworks = GetTargetFrameworks(buildContext, projectName);
    testTargetFramework = targetFrameworks.FirstOrDefault();

    buildContext.CakeContext.Information("Auto detected test target framework '{0}'", testTargetFramework);

    if (string.IsNullOrWhiteSpace(testTargetFramework))
    {
        throw new Exception(string.Format("Test target framework could not automatically be detected for project '{0]'", projectName));
    }

    return testTargetFramework;
}