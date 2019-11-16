#l "generic-variables.cake"

#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"
#addin "nuget:?package=Cake.DependencyCheck&version=1.2.0"

#tool "nuget:?package=DependencyCheck.Runner.Tool&include=./**/dependency-check.sh&include=./**/dependency-check.bat"
#tool "nuget:?package=JetBrains.ReSharper.CommandLineTools&version=2018.1.3"

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

    // DotNetCoreTool(null, "format", string.Join(" ", arguments),
    //     new DotNetCoreToolSettings
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
    Information("Making sure NuGet is using the latest version");

    var nuGetExecutable = buildContext.General.NuGet.Executable;

    var exitCode = StartProcess(nuGetExecutable, new ProcessSettings
    {
        Arguments = "update -self"
    });

    var newNuGetVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(nuGetExecutable);
    var newNuGetVersion = newNuGetVersionInfo.FileVersion;

    Information("Updating NuGet.exe exited with '{0}', version is '{1}'", exitCode, newNuGetVersion);
});

//-------------------------------------------------------------

Task("RestorePackages")
    .IsDependentOn("UpdateNuGet")
    .ContinueOnError()
    .Does<BuildContext>(buildContext =>
{
    // var csharpProjects = GetFiles("./**/*.csproj");
    // var cProjects = GetFiles("./**/*.vcxproj");
    var solutions = GetFiles("./**/*.sln");
    
    var allFiles = new List<FilePath>();
    // //allFiles.AddRange(projects);
    // //allFiles.AddRange(cProjects);
    allFiles.AddRange(solutions);

    foreach(var file in allFiles)
    {
        RestoreNuGetPackages(buildContext, file);
    }

    foreach (var project in buildContext.AllProjects)
    {
        var projectFileName = GetProjectFileName(buildContext, project);
        RestoreNuGetPackages(buildContext, projectFileName);
    }
});

//-------------------------------------------------------------

// Note: it might look weird that this is dependent on restore packages,
// but to clean, the msbuild projects must be able to load. However, they need
// some targets files that come in via packages

Task("Clean")
    //.IsDependentOn("RestorePackages")
    .ContinueOnError()
    .Does<BuildContext>(buildContext => 
{
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

    var directoriesToDelete = new List<string>();

    // Output directory
    directoriesToDelete.Add(buildContext.General.OutputRootDirectory);

    // obj directories
    foreach (var project in buildContext.AllProjects)
    {
        var projectDirectory = GetProjectDirectory(project);

        Information($"Investigating paths to clean up in '{projectDirectory}'");

        var binDirectory = System.IO.Path.Combine(projectDirectory, "bin");
        directoriesToDelete.Add(binDirectory);

        var objDirectory = System.IO.Path.Combine(projectDirectory, "obj");
        directoriesToDelete.Add(objDirectory);

        // Special C++ scenarios
        var projectFileName = GetProjectFileName(buildContext, project);
        if (IsCppProject(projectFileName))
        {
            var debugDirectory = System.IO.Path.Combine(projectDirectory, "Debug");
            directoriesToDelete.Add(debugDirectory);

            var releaseDirectory = System.IO.Path.Combine(projectDirectory, "Release");
            directoriesToDelete.Add(releaseDirectory);

            var x64Directory = System.IO.Path.Combine(projectDirectory, "x64");
            directoriesToDelete.Add(x64Directory);

            var x86Directory = System.IO.Path.Combine(projectDirectory, "x86");
            directoriesToDelete.Add(x86Directory);
        }
    }

    foreach (var directoryToDelete in directoriesToDelete)
    {
        if (DirectoryExists(directoryToDelete))
        {
            Information($"Cleaning up directory '{directoryToDelete}'");

            DeleteDirectory(directoryToDelete, new DeleteDirectorySettings()
            {
                Force = true,
                Recursive = true
            });
        }
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
    .ContinueOnError()
    .Does<BuildContext>(buildContext =>
{
    if (buildContext.General.IsCiBuild)
    {
        Information("Skipping code signing because this is a CI build");
        return;
    }

    if (buildContext.General.IsLocalBuild)
    {
        Information("Skipping code signing because this is a local package build");
        return;
    }

    var certificateSubjectName = buildContext.General.CodeSign.CertificateSubjectName;
    if (string.IsNullOrWhiteSpace(certificateSubjectName))
    {
        Information("Skipping code signing because the certificate subject name was not specified");
        return;
    }

    List<FilePath> filesToSign = new List<FilePath>();

    // Note: only code-sign components & wpf apps, skip test projects & uwp apps
    var projectsToCodeSign = new List<string>();
    projectsToCodeSign.AddRange(buildContext.Components.Items);
    projectsToCodeSign.AddRange(buildContext.Wpf.Items);

    foreach (var projectToCodeSign in projectsToCodeSign)
    {
        var codeSignWildCard = buildContext.General.CodeSign.WildCard;
        if (string.IsNullOrWhiteSpace(codeSignWildCard))
        {
            // Empty, we need to override with project name for valid default value
            codeSignWildCard = projectToCodeSign;
        }
    
        var projectFilesToSign = new List<FilePath>();

        var outputDirectory = string.Format("{0}/{1}", buildContext.General.OutputRootDirectory, projectToCodeSign);

        var exeSignFilesSearchPattern = string.Format("{0}/**/*{1}*.exe", outputDirectory, codeSignWildCard);
        Information(exeSignFilesSearchPattern);
        projectFilesToSign.AddRange(GetFiles(exeSignFilesSearchPattern));

        var dllSignFilesSearchPattern = string.Format("{0}/**/*{1}*.dll", outputDirectory, codeSignWildCard);
        Information(dllSignFilesSearchPattern);
        projectFilesToSign.AddRange(GetFiles(dllSignFilesSearchPattern));

        Information("Found '{0}' files to code sign for '{1}'", projectFilesToSign.Count, projectToCodeSign);

        filesToSign.AddRange(projectFilesToSign);
    }

    if (filesToSign.Count == 0)
    {
        Information("Found no files to sign, skipping code signing process...");
        return;
    }

    Information("Found '{0}' files to code sign using subject name '{1}', this can take a few minutes...", filesToSign.Count, certificateSubjectName);

    var signToolSignSettings = new SignToolSignSettings 
    {
        AppendSignature = false,
        TimeStampUri = new Uri(buildContext.General.CodeSign.TimeStampUri),
        CertSubjectName = certificateSubjectName
    };

    Sign(filesToSign, signToolSignSettings);

    // Note parallel doesn't seem to be faster in an example repository:
    // 1 thread:   1m 30s
    // 4 threads:  1m 30s
    // 10 threads: 1m 30s
    // Parallel.ForEach(filesToSign, new ParallelOptions 
    //     { 
    //         MaxDegreeOfParallelism = 10 
    //     },
    //     fileToSign => 
    //     { 
    //         Sign(fileToSign, signToolSignSettings);
    //     });
});