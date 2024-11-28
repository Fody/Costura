#l "generic-variables.cake"

//#addin "nuget:?package=Cake.DependencyCheck&version=1.2.0"

//#tool "nuget:?package=DependencyCheck.Runner.Tool&version=3.2.1&include=./**/dependency-check.sh&include=./**/dependency-check.bat"

//-------------------------------------------------------------

private void ValidateRequiredInput(string parameterName)
{
    // TODO: Do we want to check the configuration as well?

    if (!Parameters.ContainsKey(parameterName))
    {
        throw new Exception(string.Format("Parameter '{0}' is required but not defined", parameterName));
    }
}

//-------------------------------------------------------------

private void CleanUpCode(bool failOnChanges)
{
    Information("Cleaning up code using dotnet-format");

    // --check: return non-0 exit code if changes are needed
    // --dry-run: don't save files

    // Note: disabled for now, see:
    // * https://github.com/onovotny/MSBuildSdkExtras/issues/164
    // * https://github.com/microsoft/msbuild/issues/4376
    // var arguments = new List<string>();

    // //arguments.Add("--dry-run");

    // if (failOnChanges)
    // {
    //     arguments.Add("--check");
    // }

    // DotNetTool(null, "format", string.Join(" ", arguments),
    //     new DotNetToolSettings
    //     {
    //         WorkingDirectory = "./src/"
    //     });
}

//-------------------------------------------------------------

private void VerifyDependencies(string pathToScan = "./src/**/*.csproj")
{
    Information("Verifying dependencies for security vulnerabilities in '{0}'", pathToScan);

    // Disabled for now
    //DependencyCheck(new DependencyCheckSettings
    //{
    //    Project = SolutionName,
    //    Scan = pathToScan,
    //    FailOnCVSS = "0",
    //    Format = "HTML",
    //    Data = "%temp%/dependency-check/data"
    //});
}

//-------------------------------------------------------------

private void UpdateSolutionAssemblyInfo(BuildContext buildContext)
{
    Information("Updating assembly info to '{0}'", buildContext.General.Version.FullSemVer);

    var assemblyInfoParseResult = ParseAssemblyInfo(buildContext.General.Solution.AssemblyInfoFileName);

    var assemblyInfo = new AssemblyInfoSettings 
    {
        Company = buildContext.General.Copyright.Company,
        Version = buildContext.General.Version.MajorMinorPatch,
        FileVersion = buildContext.General.Version.MajorMinorPatch,
        InformationalVersion = buildContext.General.Version.FullSemVer,
        Copyright = string.Format("Copyright © {0} {1} - {2}", 
            buildContext.General.Copyright.Company, buildContext.General.Copyright.StartYear, DateTime.Now.Year)
    };

    CreateAssemblyInfo(buildContext.General.Solution.AssemblyInfoFileName, assemblyInfo);
}

//-------------------------------------------------------------

Task("UpdateNuGet")
    .ContinueOnError()
    .Does<BuildContext>(buildContext => 
{
    // DISABLED UNTIL NUGET GETS FIXED: https://github.com/NuGet/Home/issues/10853

    // Information("Making sure NuGet is using the latest version");

    // if (buildContext.General.IsLocalBuild && buildContext.General.MaximizePerformance)
    // {
    //     Information("Local build with maximized performance detected, skipping NuGet update check");
    //     return;
    // }

    // var nuGetExecutable = buildContext.General.NuGet.Executable;

    // var exitCode = StartProcess(nuGetExecutable, new ProcessSettings
    // {
    //     Arguments = "update -self"
    // });

    // var newNuGetVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(nuGetExecutable);
    // var newNuGetVersion = newNuGetVersionInfo.FileVersion;

    // Information("Updating NuGet.exe exited with '{0}', version is '{1}'", exitCode, newNuGetVersion);
});

//-------------------------------------------------------------

Task("RestorePackages")
    .IsDependentOn("Prepare")
    .IsDependentOn("UpdateNuGet")
    .ContinueOnError()
    .Does<BuildContext>(buildContext =>
{
    if (buildContext.General.IsLocalBuild && buildContext.General.MaximizePerformance)
    {
        Information("Local build with maximized performance detected, skipping package restore");
        return;
    }

    //var csharpProjects = GetFiles("./**/*.csproj");
    // var cProjects = GetFiles("./**/*.vcxproj");
    var solutions = GetFiles("./**/*.sln");
    var csharpProjects = new List<FilePath>();

    foreach (var project in buildContext.AllProjects)
    {
        // Once a project is in AllProjects, it should always be restored
        
        var projectFileName = GetProjectFileName(buildContext, project);
        if (projectFileName.EndsWith(".csproj"))
        {
            Information("Adding '{0}' as C# specific project to restore", project);

            csharpProjects.Add(projectFileName);

            // Inject source link *before* package restore
            InjectSourceLinkInProjectFile(buildContext, project, projectFileName);
        }
    }

    var allFiles = new List<FilePath>();
    //allFiles.AddRange(solutions);
    allFiles.AddRange(csharpProjects);
    // //allFiles.AddRange(cProjects);

	Information($"Found '{allFiles.Count}' projects to restore");

    foreach (var file in allFiles)
    {
        RestoreNuGetPackages(buildContext, file);
    }

    // C++ files need to be done manually
    foreach (var project in buildContext.AllProjects)
    {
        var projectFileName = GetProjectFileName(buildContext, project);
        if (IsCppProject(projectFileName))
        {
            buildContext.CakeContext.LogSeparator("'{0}' is a C++ project, restoring NuGet packages separately", project);

            RestoreNuGetPackages(buildContext, projectFileName);

            // For C++ projects, we must clean the project again after a package restore
            CleanProject(buildContext, project);
        }
    }
});

//-------------------------------------------------------------

// Note: it might look weird that this is dependent on restore packages,
// but to clean, the msbuild projects must be able to load. However, they need
// some targets files that come in via packages

Task("Clean")
    //.IsDependentOn("RestorePackages")
    .IsDependentOn("Prepare")
    .ContinueOnError()
    .Does<BuildContext>(buildContext => 
{
    if (buildContext.General.IsLocalBuild && buildContext.General.MaximizePerformance)
    {
        Information("Local build with maximized performance detected, skipping solution clean");
        return;
    }

    var platforms = new Dictionary<string, PlatformTarget>();
    platforms["AnyCPU"] = PlatformTarget.MSIL;
    platforms["x86"] = PlatformTarget.x86;
    platforms["x64"] = PlatformTarget.x64;
    platforms["arm"] = PlatformTarget.ARM;

    foreach (var platform in platforms)
    {
        try
        {
            Information("Cleaning output for platform '{0}'", platform.Value);

            var msBuildSettings = new MSBuildSettings
            {
                Verbosity = Verbosity.Minimal,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = buildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = platform.Value
            };

            ConfigureMsBuild(buildContext, msBuildSettings, platform.Key, "clean");

            msBuildSettings.Targets.Add("Clean");

            MSBuild(buildContext.General.Solution.FileName, msBuildSettings);
        }
        catch (System.Exception ex)
        {
            Warning("Failed to clean output for platform '{0}': {1}", platform.Value, ex.Message);
        }
    }

    // Output directory
    DeleteDirectoryWithLogging(buildContext, buildContext.General.OutputRootDirectory);

    // obj directories
    foreach (var project in buildContext.AllProjects)
    {
        CleanProject(buildContext, project);
    }
});

//-------------------------------------------------------------

Task("VerifyDependencies")
    .IsDependentOn("Prepare")
    .Does(async () =>
{
    // if (DependencyCheckDisabled)
    // {
    //     Information("Dependency analysis is disabled");
    //     return;
    // }

    // VerifyDependencies();
});

//-------------------------------------------------------------

Task("CleanupCode")
    .Does<BuildContext>(buildContext => 
{
    CleanUpCode(true);
});

//-------------------------------------------------------------

Task("CodeSign")
    //.ContinueOnError()
    .Does<BuildContext>(buildContext =>
{
    if (buildContext.General.IsCiBuild)
    {
        Information("Skipping code signing because this is a CI build");
        return;
    }

    if (buildContext.General.IsLocalBuild)
    {
        Information("Local build detected, skipping code signing");
        return;
    }

    if (!buildContext.General.CodeSign.IsAvailable &&
        !buildContext.General.AzureCodeSign.IsAvailable)
    {
        Information("Skipping code signing since no option is available");
        return;
    }

    var filesToSign = new List<FilePath>();

    // Note: only code-sign components & wpf apps, skip test projects & uwp apps
    var projectsToCodeSign = new List<string>();
    projectsToCodeSign.AddRange(buildContext.Components.Items);
    projectsToCodeSign.AddRange(buildContext.Wpf.Items);

    foreach (var projectToCodeSign in projectsToCodeSign)
    {
        SignProjectFiles(buildContext, projectToCodeSign);
    }
});