// Customize this file when using a different test framework
#l "tests-variables.cake"
#l "tests-nunit.cake"

//-------------------------------------------------------------

private static void BuildTestProjects(BuildContext buildContext)
{
    // In case of a local build and we have included / excluded anything, skip tests
    if (buildContext.General.IsLocalBuild && 
        (buildContext.General.Includes.Count > 0 || buildContext.General.Excludes.Count > 0))
    {
        buildContext.CakeContext.Information("Skipping test project because this is a local build with specific includes / excludes");
        return;
    }

    foreach (var testProject in buildContext.Tests.Items)
    {
        buildContext.CakeContext.LogSeparator("Building test project '{0}'", testProject);

        var projectFileName = GetProjectFileName(buildContext, testProject);
        
        var msBuildSettings = new MSBuildSettings
        {
            Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
            ToolVersion = MSBuildToolVersion.Default,
            Configuration = buildContext.General.Solution.ConfigurationName,
            MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
            PlatformTarget = PlatformTarget.MSIL
        };

        ConfigureMsBuild(buildContext, msBuildSettings, testProject);

        // Always disable SourceLink
        msBuildSettings.WithProperty("EnableSourceLink", "false");

        // Force disable SonarQube
        msBuildSettings.WithProperty("SonarQubeExclude", "true");

        // Note: we need to set OverridableOutputPath because we need to be able to respect
        // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
        // are properties passed in using the command line)
        var outputDirectory = GetProjectOutputDirectory(buildContext, testProject);
        buildContext.CakeContext.Information("Output directory: '{0}'", outputDirectory);
        msBuildSettings.WithProperty("OverridableOutputRootPath", buildContext.General.OutputRootDirectory);
        msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
        msBuildSettings.WithProperty("PackageOutputPath", buildContext.General.OutputRootDirectory);

        RunMsBuild(buildContext, testProject, projectFileName, msBuildSettings);
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

            buildContext.CakeContext.DotNetCoreTest(projectFileName, new DotNetCoreTestSettings
            {
                Configuration = buildContext.General.Solution.ConfigurationName,
                NoBuild = true,
                NoLogo = true,
                NoRestore = true,
                OutputDirectory = System.IO.Path.Combine(GetProjectOutputDirectory(buildContext, projectName), testTargetFramework),
                ResultsDirectory = testResultsDirectory
            });

            // Information("Project '{0}' is a .NET core project, using 'dotnet vstest' to run the unit tests", projectName); 

            // var testFile = string.Format("{0}/{1}/{2}.dll", GetProjectOutputDirectory(buildContext, projectName), testTargetFramework, projectName);

            // DotNetCoreVSTest(testFile, new DotNetCoreVSTestSettings
            // {
            //     //Platform = TestFramework
            //     ResultsDirectory = testResultsDirectory
            // });

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