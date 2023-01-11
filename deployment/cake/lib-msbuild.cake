#addin "nuget:?package=Cake.Issues&version=2.0.0"
#addin "nuget:?package=Cake.Issues.MsBuild&version=2.0.0"

#tool "nuget:?package=MSBuild.Extension.Pack&version=1.9.1"

//-------------------------------------------------------------

private static void BuildSolution(BuildContext buildContext)
{
    var solutionName = buildContext.General.Solution.Name;
    var solutionFileName = buildContext.General.Solution.FileName;

    buildContext.CakeContext.LogSeparator("Building solution '{0}'", solutionName);

    var msBuildSettings = new MSBuildSettings 
    {
        Verbosity = Verbosity.Quiet,
        //Verbosity = Verbosity.Diagnostic,
        ToolVersion = MSBuildToolVersion.Default,
        Configuration = buildContext.General.Solution.ConfigurationName,
        MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform,
        PlatformTarget = PlatformTarget.MSIL,
        NoLogo = true
    };

    //ConfigureMsBuild(buildContext, msBuildSettings, dependency, "build");

    RunMsBuild(buildContext, "Solution", solutionFileName, msBuildSettings, "build");
}

//-------------------------------------------------------------

private static void ConfigureMsBuild(BuildContext buildContext, MSBuildSettings msBuildSettings, 
    string projectName, string action, bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(buildContext, allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        buildContext.CakeContext.Information($"Overriding ms build tool path to '{toolPath}'");

        msBuildSettings.ToolPath = toolPath;
    }

    // Note: we need to set OverridableOutputPath because we need to be able to respect
    // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
    // are properties passed in using the command line)
    var outputDirectory = GetProjectOutputDirectory(buildContext, projectName);
    buildContext.CakeContext.Information("Output directory: '{0}'", outputDirectory);
    msBuildSettings.WithProperty("OverridableOutputRootPath", buildContext.General.OutputRootDirectory);
                
    // GHK: 2022-05-25: Disabled overriding the (whole) output path since this caused all 
    // reference projects to be re-build again since this override is used for all projects, 
    // including project references
    //msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);

    msBuildSettings.WithProperty("PackageOutputPath", buildContext.General.OutputRootDirectory);

    // Only optimize in release mode
    if (!buildContext.General.IsLocalBuild)
    {
        buildContext.CakeContext.Information($"This is NOT a local build, disabling building of project references");

        // Don't build project references (should already be built)
        msBuildSettings.WithProperty("BuildProjectReferences", "false");

        //InjectAssemblySearchPathsInProjectFile(buildContext, projectName, GetProjectFileName(buildContext, projectName));
    }
    else
    {
        buildContext.CakeContext.Information($"This is a local build, disabling building of project references");
    }

    // Continuous integration build
    msBuildSettings.ContinuousIntegrationBuild = true;
    //msBuildSettings.WithProperty("ContinuousIntegrationBuild", "true");

    // No NuGet restore (should already be done)
    msBuildSettings.WithProperty("ResolveNuGetPackages", "false");
    msBuildSettings.Restore = false;

    // Solution info
    // msBuildSettings.WithProperty("SolutionFileName", System.IO.Path.GetFileName(buildContext.General.Solution.FileName));
    // msBuildSettings.WithProperty("SolutionPath", System.IO.Path.GetFullPath(buildContext.General.Solution.FileName));
    // msBuildSettings.WithProperty("SolutionDir", System.IO.Path.GetFullPath(buildContext.General.Solution.Directory));
    // msBuildSettings.WithProperty("SolutionName", buildContext.General.Solution.Name);
    // msBuildSettings.WithProperty("SolutionExt", ".sln");
    // msBuildSettings.WithProperty("DefineExplicitDefaults", "true");

    // Disable copyright info
    msBuildSettings.NoLogo = true;

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLogger
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}_log.log", projectName, action))
    });

    // Enable for bin logging
    msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    {
        Enabled = true,
        Imports = MSBuildBinaryLogImports.Embed,
        FileName = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}_log.binlog", projectName, action))
    };
}

//-------------------------------------------------------------

private static void ConfigureMsBuildForDotNet(BuildContext buildContext, DotNetMSBuildSettings msBuildSettings, 
    string projectName, string action, bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(buildContext, allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        buildContext.CakeContext.Information($"Overriding ms build tool path to '{toolPath}'");

        msBuildSettings.ToolPath = toolPath;
    }

    // Note: we need to set OverridableOutputPath because we need to be able to respect
    // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
    // are properties passed in using the command line)
    var outputDirectory = GetProjectOutputDirectory(buildContext, projectName);
    buildContext.CakeContext.Information("Output directory: '{0}'", outputDirectory);
    msBuildSettings.WithProperty("OverridableOutputRootPath", buildContext.General.OutputRootDirectory);
                
    // GHK: 2022-05-25: Disabled overriding the (whole) output path since this caused all 
    // reference projects to be re-build again since this override is used for all projects, 
    // including project references
    //msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);

    msBuildSettings.WithProperty("PackageOutputPath", buildContext.General.OutputRootDirectory);

    // Only optimize in release mode
    if (!buildContext.General.IsLocalBuild)
    {
        buildContext.CakeContext.Information($"This is NOT a local build, disabling building of project references");

        // Don't build project references (should already be built)
        msBuildSettings.WithProperty("BuildProjectReferences", "false");

        //InjectAssemblySearchPathsInProjectFile(buildContext, projectName, GetProjectFileName(buildContext, projectName));
    }
    else
    {
        buildContext.CakeContext.Information($"This is a local build, disabling building of project references");
    }

    // Continuous integration build
    msBuildSettings.ContinuousIntegrationBuild = true;
    //msBuildSettings.WithProperty("ContinuousIntegrationBuild", "true");

    // No NuGet restore (should already be done)
    msBuildSettings.WithProperty("ResolveNuGetPackages", "false");
    //msBuildSettings.Restore = false;

    // Solution info
    // msBuildSettings.WithProperty("SolutionFileName", System.IO.Path.GetFileName(buildContext.General.Solution.FileName));
    // msBuildSettings.WithProperty("SolutionPath", System.IO.Path.GetFullPath(buildContext.General.Solution.FileName));
    // msBuildSettings.WithProperty("SolutionDir", System.IO.Path.GetFullPath(buildContext.General.Solution.Directory));
    // msBuildSettings.WithProperty("SolutionName", buildContext.General.Solution.Name);
    // msBuildSettings.WithProperty("SolutionExt", ".sln");
    // msBuildSettings.WithProperty("DefineExplicitDefaults", "true");

    // Disable copyright info
    msBuildSettings.NoLogo = true;

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLoggerSettings
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}_log.log", projectName, action))
    });

    // Enable for bin logging
    msBuildSettings.BinaryLogger = new MSBuildBinaryLoggerSettings
    {
        Enabled = true,
        Imports = MSBuildBinaryLoggerImports.Embed,
        FileName = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    };
    
    // Note: this only works for direct .net core msbuild usage, not when this is
    // being wrapped in a tool (such as 'dotnet pack')
    var binLogArgs = string.Format("-bl:\"{0}\";ProjectImports=Embed", 
        System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}_log.binlog", projectName, action)));

    msBuildSettings.ArgumentCustomization = args => args.Append(binLogArgs);
}

//-------------------------------------------------------------

private static void RunMsBuild(BuildContext buildContext, string projectName, string projectFileName, MSBuildSettings msBuildSettings, string action)
{
    // IMPORTANT NOTE --- READ  <=============================================
    //
    // Note:
    // - Binary logger outputs version 9, but the binlog reader only supports up to 8
    // - Xml logger only seems to read warnings
    //
    // IMPORTANT NOTE --- READ  <=============================================

    var totalStopwatch = Stopwatch.StartNew();
    var buildStopwatch = Stopwatch.StartNew();

    // Enforce additional logging for issues
    //var logPath = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}_log.binlog", projectName, action));

    buildContext.CakeContext.CreateDirectory(buildContext.General.OutputRootDirectory);

    var logPath = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}_log.xml", projectName, action));
    msBuildSettings.WithLogger(buildContext.CakeContext.Tools.Resolve("MSBuild.ExtensionPack.Loggers.dll").FullPath, 
        "XmlFileLogger", $"logfile=\"{logPath}\";verbosity=Detailed;encoding=UTF-8");

    var failBuild = false;

    try
    {        
        // using (buildContext.CakeContext.UseDiagnosticVerbosity())
        // {
            buildContext.CakeContext.MSBuild(projectFileName, msBuildSettings);
        //}
    }
    catch (System.Exception)
    {
        // Accept for now, we will throw later
        failBuild = true;
    }

    buildContext.CakeContext.Information(string.Empty);
    buildContext.CakeContext.Information($"Done {action}ing project, took '{buildStopwatch.Elapsed}'");
    buildContext.CakeContext.Information(string.Empty);
    buildContext.CakeContext.Information($"Investigating potential issues using '{logPath}'");
    buildContext.CakeContext.Information(string.Empty);
    
    var investigationStopwatch = Stopwatch.StartNew();

    var issuesContext = buildContext.CakeContext.MsBuildIssuesFromFilePath(logPath, buildContext.CakeContext.MsBuildXmlFileLoggerFormat());
    //var issuesContext = buildContext.CakeContext.MsBuildIssuesFromFilePath(logPath, buildContext.CakeContext.MsBuildBinaryLogFileFormat());

    buildContext.CakeContext.Debug("Created issue context");

    var issues = buildContext.CakeContext.ReadIssues(issuesContext, buildContext.General.RootDirectory);

    buildContext.CakeContext.Debug($"Found '{issues.Count()}' potential issues");

    buildContext.CakeContext.Information(string.Empty);

    var loggedIssues = new HashSet<string>();

    foreach (var issue in issues)
    {
        var priority = issue.Priority ?? 0;

        var message = $"{issue.AffectedFileRelativePath}({issue.Line},{issue.Column}): {issue.Rule}: {issue.MessageText}";
        if (loggedIssues.Contains(message))
        {
            continue;
        }

        //buildContext.CakeContext.Information($"[{issue.Priority}] {message}");

        if (priority == (int)IssuePriority.Warning)
        {
            buildContext.CakeContext.Warning($"WARNING: {message}");

            loggedIssues.Add(message);
        }
        else if (priority == (int)IssuePriority.Error)
        {
            buildContext.CakeContext.Error($"ERROR: {message}");

            loggedIssues.Add(message);

            failBuild = true;
        }
    }

    buildContext.CakeContext.Information(string.Empty);
    buildContext.CakeContext.Information($"Done investigating project, took '{investigationStopwatch.Elapsed}'");
    buildContext.CakeContext.Information($"Total msbuild ({action} + investigation) took '{totalStopwatch.Elapsed}'");
    buildContext.CakeContext.Information(string.Empty);

    if (failBuild)
    {    
        buildContext.CakeContext.Information(string.Empty);

        throw new Exception($"{action} failed for project '{projectName}'");
    }
}

//-------------------------------------------------------------

private static string FindLatestWindowsKitsDirectory(BuildContext buildContext)
{
    // Find highest number with 10.0, e.g. 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64\makeappx.exe'
    var directories = buildContext.CakeContext.GetDirectories($@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}/Windows Kits/10/bin/10.0.*");
    
    //buildContext.CakeContext.Information($"Found '{directories.Count}' potential directories for MakeAppX.exe");

    var directory = directories.LastOrDefault();
    if (directory != null)
    {
        return directory.FullPath;
    }

    return null;
}

//-------------------------------------------------------------

private static string GetVisualStudioDirectory(BuildContext buildContext, bool? allowVsPrerelease = null)
{
    // TODO: Support different editions (e.g. Professional, Enterprise, Community, etc)

    // Force 64-bit, even when running as 32-bit process
    var programFilesx64 = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
    var programFilesx86 = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");

    var prereleasePaths = new List<KeyValuePair<string, string>>(new [] 
    { 
        new KeyValuePair<string, string>("Visual Studio 2022 Preview", $@"{programFilesx64}\Microsoft Visual Studio\2022\Preview\"),
        new KeyValuePair<string, string>("Visual Studio 2019 Preview", $@"{programFilesx86}\Microsoft Visual Studio\2019\Preview\"),
    });

    var normalPaths = new List<KeyValuePair<string, string>> (new []
    {
        new KeyValuePair<string, string>("Visual Studio 2022 Enterprise", $@"{programFilesx64}\Microsoft Visual Studio\2022\Enterprise\"),
        new KeyValuePair<string, string>("Visual Studio 2022 Professional", $@"{programFilesx64}\Microsoft Visual Studio\2022\Professional\"),
        new KeyValuePair<string, string>("Visual Studio 2022 Community", $@"{programFilesx64}\Microsoft Visual Studio\2022\Community\"),
        new KeyValuePair<string, string>("Visual Studio 2019 Enterprise", $@"{programFilesx86}\Microsoft Visual Studio\2019\Enterprise\"),
        new KeyValuePair<string, string>("Visual Studio 2019 Professional", $@"{programFilesx86}\Microsoft Visual Studio\2019\Professional\"),
        new KeyValuePair<string, string>("Visual Studio 2019 Community", $@"{programFilesx86}\Microsoft Visual Studio\2019\Community\"),
    });

    // Prerelease paths
    if ((allowVsPrerelease ?? true) && buildContext.General.UseVisualStudioPrerelease)
    {
        buildContext.CakeContext.Debug("Checking for installation of Visual Studio (preview)");

        foreach (var prereleasePath in prereleasePaths)
        {
            if (System.IO.Directory.Exists(prereleasePath.Value))
            {
                buildContext.CakeContext.Debug($"Found {prereleasePath.Key}");

                return prereleasePath.Value;
            }
        }
    }

    buildContext.CakeContext.Debug("Checking for installation of Visual Studio (non-preview)");
    
    // Normal paths
    foreach (var normalPath in normalPaths)
    {
        if (System.IO.Directory.Exists(normalPath.Value))
        {
            buildContext.CakeContext.Debug($"Found {normalPath.Key}");

            return normalPath.Value;
        }
    }

    // Fallback in case someone *only* has prerelease
    foreach (var prereleasePath in prereleasePaths)
    {
        if (System.IO.Directory.Exists(prereleasePath.Value))
        {
            buildContext.CakeContext.Information($"Only Visual Studio preview is available, using {prereleasePath.Key}");

            return prereleasePath.Value;
        }
    } 

    // Failed
    return null;
}

//-------------------------------------------------------------

private static string GetVisualStudioPath(BuildContext buildContext, bool? allowVsPrerelease = null)
{
    var potentialPaths = new []
    {
        @"MSBuild\Current\Bin\msbuild.exe",
        @"MSBuild\17.0\Bin\msbuild.exe",
        @"MSBuild\16.0\Bin\msbuild.exe",
        @"MSBuild\15.0\Bin\msbuild.exe"
    };

    var directory = GetVisualStudioDirectory(buildContext, allowVsPrerelease);

    foreach (var potentialPath in potentialPaths)
    {
        var pathToCheck = System.IO.Path.Combine(directory, potentialPath);
        if (System.IO.File.Exists(pathToCheck))
        {
            return pathToCheck;
        }
    }

    throw new Exception("Could not find the path to Visual Studio (msbuild.exe)");
}

//-------------------------------------------------------------

private static void InjectAssemblySearchPathsInProjectFile(BuildContext buildContext, string projectName, string projectFileName)
{
    try
    {
        // Allow this project to find any other projects that we have built (since we disabled
        // building of project dependencies)
        var assemblySearchPaths = new List<string>();
        var separator = System.IO.Path.DirectorySeparatorChar.ToString();

        foreach (var project in buildContext.AllProjects)
        {
            var projectOutputDirectory = GetProjectOutputDirectory(buildContext, project);
            assemblySearchPaths.Add(projectOutputDirectory);
        }

        if (assemblySearchPaths.Count == 0)
        {
            buildContext.CakeContext.Information("No assembly search paths found to inject");
            return;
        }        

        // For SourceLink to work, the .csproj should contain something like this:
        // <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0-beta-63127-02" PrivateAssets="all" />
        var projectFileContents = System.IO.File.ReadAllText(projectFileName);
        if (projectFileContents.Contains("AssemblySearchPaths"))
        {
            buildContext.CakeContext.Information("Assembly search paths is already added to the project file");
            return;
        }

        buildContext.CakeContext.Information("Injecting assembly search paths into project file");

        var xmlDocument = XDocument.Parse(projectFileContents);
        var projectElement = xmlDocument.Root;

        // Item group with package reference
        var propertyGroupElement = new XElement("PropertyGroup");
        var assemblySearchPathsElement = new XElement("AssemblySearchPaths");

        assemblySearchPathsElement.Value = $"$(AssemblySearchPaths);{string.Join(";", assemblySearchPaths)}";

        propertyGroupElement.Add(assemblySearchPathsElement);
        projectElement.Add(propertyGroupElement);

        xmlDocument.Save(projectFileName);
    }
    catch (Exception ex)
    {
        buildContext.CakeContext.Error($"Failed to process assembly search paths for project '{projectFileName}': {ex.Message}");
    }
}