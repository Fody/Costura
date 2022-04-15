#l "buildserver.cake"

#tool "nuget:?package=GitVersion.CommandLine&version=5.10.1"

//-------------------------------------------------------------

public class GeneralContext : BuildContextWithItemsBase
{
    public GeneralContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
        SkipComponentsThatAreNotDeployable = true;
    }

    public string Target { get; set; }
    public string RootDirectory { get; set; }
    public string OutputRootDirectory { get; set; }

    public bool IsCiBuild { get; set; }
    public bool IsAlphaBuild { get; set; }
    public bool IsBetaBuild { get; set; }
    public bool IsOfficialBuild { get; set; }
    public bool IsLocalBuild { get; set; }
    public bool MaximizePerformance { get; set; }
    public bool UseVisualStudioPrerelease { get; set; }
    public bool VerifyDependencies { get; set; }
    public bool SkipComponentsThatAreNotDeployable { get; set; }

    public VersionContext Version { get; set; }
    public CopyrightContext Copyright { get; set; }
    public NuGetContext NuGet { get; set; }
    public SolutionContext Solution { get; set; }
    public SourceLinkContext SourceLink { get; set; }
    public CodeSignContext CodeSign { get; set; }
    public RepositoryContext Repository { get; set; }
    public SonarQubeContext SonarQube { get; set; }

    public List<string> Includes { get; set; }
    public List<string> Excludes { get; set; }

    protected override void ValidateContext()
    {
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Running target '{Target}'");
        CakeContext.Information($"Using output directory '{OutputRootDirectory}'");
    }
}

//-------------------------------------------------------------

public class VersionContext : BuildContextBase
{
    private GitVersion _gitVersionContext;

    public VersionContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public GitVersion GetGitVersionContext(GeneralContext generalContext)
    {
        if (_gitVersionContext is null)
        {
            var gitVersionSettings = new GitVersionSettings
            {
                UpdateAssemblyInfo = false,
                Verbosity = GitVersionVerbosity.Verbose
            };

            var gitDirectory = ".git";
            if (!CakeContext.DirectoryExists(gitDirectory))
            {
                CakeContext.Information("No local .git directory found, treating as dynamic repository");

                // Make a *BIG* assumption that the solution name == repository name
                var repositoryName = generalContext.Solution.Name;
                var dynamicRepositoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), repositoryName);

                if (ClearCache)
                {
                    CakeContext.Warning("Cleaning the cloned temp directory, disable by setting 'GitVersion_ClearCache' to 'false'");
    
                    if (CakeContext.DirectoryExists(dynamicRepositoryPath))
                    {
                        CakeContext.DeleteDirectory(dynamicRepositoryPath, new DeleteDirectorySettings
                        {
                            Force = true,
                            Recursive = true
                        });
                    }
                }

                // Validate first
                if (string.IsNullOrWhiteSpace(generalContext.Repository.BranchName))
                {
                    throw new Exception("No local .git directory was found, but repository branch was not specified either. Make sure to specify the branch");
                }

                if (string.IsNullOrWhiteSpace(generalContext.Repository.Url))
                {
                    throw new Exception("No local .git directory was found, but repository url was not specified either. Make sure to specify the branch");
                }

                CakeContext.Information($"Fetching dynamic repository from url '{generalContext.Repository.Url}' => '{dynamicRepositoryPath}'");

                // Dynamic repository
                gitVersionSettings.UserName = generalContext.Repository.Username;
                gitVersionSettings.Password = generalContext.Repository.Password;
                gitVersionSettings.Url = generalContext.Repository.Url;
                gitVersionSettings.Branch = generalContext.Repository.BranchName;
                gitVersionSettings.Commit = generalContext.Repository.CommitId;
                gitVersionSettings.NoFetch = false;
                gitVersionSettings.WorkingDirectory = generalContext.RootDirectory;
                gitVersionSettings.DynamicRepositoryPath = dynamicRepositoryPath;
                gitVersionSettings.Verbosity = GitVersionVerbosity.Verbose;
            }

            _gitVersionContext = CakeContext.GitVersion(gitVersionSettings);
        }

        return _gitVersionContext;
    }

    public bool ClearCache { get; set; }

    private string _major;

    public string Major
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_major))
            {
                _major = GetVersion(MajorMinorPatch, 1);
            }

            return _major;
        }
    }

    private string _majorMinor;

    public string MajorMinor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_majorMinor))
            {
                _majorMinor = GetVersion(MajorMinorPatch, 2);
            }

            return _majorMinor;
        }
    }

    public string MajorMinorPatch { get; set; }
    public string FullSemVer { get; set; }
    public string NuGet { get; set; }
    public string CommitsSinceVersionSource { get; set; }

    private string GetVersion(string version, int breakCount)
    {
        var finalVersion = string.Empty;

        for (int i = 0; i < version.Length; i++)
        {
            var character = version[i];
            if (!char.IsDigit(character))
            {
                breakCount--;
                if (breakCount <= 0)
                {
                    break;
                }
            }

            finalVersion += character.ToString();
        }

        return finalVersion;
    }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

public class CopyrightContext : BuildContextBase
{
    public CopyrightContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string Company { get; set; }
    public string StartYear { get; set; }

    protected override void ValidateContext()
    {
        if (string.IsNullOrWhiteSpace(Company))
        {
            throw new Exception($"Company must be defined");
        }    
    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

public class NuGetContext : BuildContextBase
{
    public NuGetContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string PackageSources { get; set; }
    public string Executable { get; set; }
    public string LocalPackagesDirectory { get; set; }

    public bool RestoreUsingNuGet { get; set; }
    public bool RestoreUsingDotNetRestore { get; set; }
    public bool NoDependencies { get; set; }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Restore using NuGet: '{RestoreUsingNuGet}'");
        CakeContext.Information($"Restore using dotnet restore: '{RestoreUsingDotNetRestore}'");
    }
}

//-------------------------------------------------------------

public class SolutionContext : BuildContextBase
{
    public SolutionContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string Name { get; set; }
    public string AssemblyInfoFileName { get; set; }
    public string FileName { get; set; }
    public string Directory
    {
        get
        {
            var directory = System.IO.Directory.GetParent(FileName).FullName;
            var separator = System.IO.Path.DirectorySeparatorChar.ToString();

            if (!directory.EndsWith(separator))
            {
                directory += separator;
            }

            return directory;
        }
    }

    public bool BuildSolution { get; set; }
    public string PublishType { get; set; }
    public string ConfigurationName { get; set; }

    protected override void ValidateContext()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new Exception($"SolutionName must be defined");
        }
    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

public class SourceLinkContext : BuildContextBase
{
    public SourceLinkContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public bool IsDisabled { get; set; }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

public class CodeSignContext : BuildContextBase
{
    public CodeSignContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string WildCard { get; set; }
    public string CertificateSubjectName { get; set; }
    public string TimeStampUri { get; set; }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

public class RepositoryContext : BuildContextBase
{
    public RepositoryContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string Url  { get; set; }
    public string BranchName  { get; set; }
    public string CommitId  { get; set; }
    public string Username  { get; set; }
    public string Password  { get; set; }

    protected override void ValidateContext()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new Exception($"RepositoryUrl must be defined");
        }
    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

public class SonarQubeContext : BuildContextBase
{
    public SonarQubeContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public bool IsDisabled { get; set; }
    public bool SupportBranches { get; set; }
    public string Url { get; set; }
    public string Organization { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Project { get; set; }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
    
    }
}

//-------------------------------------------------------------

private GeneralContext InitializeGeneralContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new GeneralContext(parentBuildContext)
    {
        Target = buildContext.BuildServer.GetVariable("Target", "Default", showValue: true),
    };

    data.Version = new VersionContext(data)
    {
        ClearCache = buildContext.BuildServer.GetVariableAsBool("GitVersion_ClearCache", false, showValue: true),
        MajorMinorPatch = buildContext.BuildServer.GetVariable("GitVersion_MajorMinorPatch", "unknown", showValue: true),
        FullSemVer = buildContext.BuildServer.GetVariable("GitVersion_FullSemVer", "unknown", showValue: true),
        NuGet = buildContext.BuildServer.GetVariable("GitVersion_NuGetVersion", "unknown", showValue: true),
        CommitsSinceVersionSource = buildContext.BuildServer.GetVariable("GitVersion_CommitsSinceVersionSource", "unknown", showValue: true)
    };

    data.Copyright = new CopyrightContext(data)
    {
        Company = buildContext.BuildServer.GetVariable("Company", showValue: true),
        StartYear = buildContext.BuildServer.GetVariable("StartYear", showValue: true)
    };

    data.NuGet = new NuGetContext(data)
    {
        PackageSources = buildContext.BuildServer.GetVariable("NuGetPackageSources", showValue: true),
        Executable = "./tools/nuget.exe",
        LocalPackagesDirectory = "c:\\source\\_packages",
        RestoreUsingNuGet = buildContext.BuildServer.GetVariableAsBool("NuGet_RestoreUsingNuGet", false, showValue: true),
        RestoreUsingDotNetRestore = buildContext.BuildServer.GetVariableAsBool("NuGet_RestoreUsingDotNetRestore", true, showValue: true),
        NoDependencies = buildContext.BuildServer.GetVariableAsBool("NuGet_NoDependencies", true, showValue: true)
    };

    var solutionName = buildContext.BuildServer.GetVariable("SolutionName", showValue: true);

    data.Solution = new SolutionContext(data)
    {
        Name = solutionName,
        AssemblyInfoFileName = "./src/SolutionAssemblyInfo.cs",
        FileName = string.Format("./src/{0}", string.Format("{0}.sln", solutionName)),
        PublishType = buildContext.BuildServer.GetVariable("PublishType", "Unknown", showValue: true),
        ConfigurationName = buildContext.BuildServer.GetVariable("ConfigurationName", "Release", showValue: true),
        BuildSolution = buildContext.BuildServer.GetVariableAsBool("BuildSolution", false, showValue: true)
    };

    data.IsCiBuild = buildContext.BuildServer.GetVariableAsBool("IsCiBuild", false, showValue: true);
    data.IsAlphaBuild = buildContext.BuildServer.GetVariableAsBool("IsAlphaBuild", false, showValue: true);
    data.IsBetaBuild = buildContext.BuildServer.GetVariableAsBool("IsBetaBuild", false, showValue: true);
    data.IsOfficialBuild = buildContext.BuildServer.GetVariableAsBool("IsOfficialBuild", false, showValue: true);
    data.IsLocalBuild = data.Target.ToLower().Contains("local");
    data.MaximizePerformance = buildContext.BuildServer.GetVariableAsBool("MaximizePerformance", true, showValue: true);
    data.UseVisualStudioPrerelease = buildContext.BuildServer.GetVariableAsBool("UseVisualStudioPrerelease", false, showValue: true);
    data.VerifyDependencies = !buildContext.BuildServer.GetVariableAsBool("DependencyCheckDisabled", false, showValue: true);
    data.SkipComponentsThatAreNotDeployable = buildContext.BuildServer.GetVariableAsBool("SkipComponentsThatAreNotDeployable", true, showValue: true);

    // If local, we want full pdb, so do a debug instead
    if (data.IsLocalBuild)
    {
        parentBuildContext.CakeContext.Warning("Enforcing configuration 'Debug' because this is seems to be a local build, do not publish this package!");
        data.Solution.ConfigurationName = "Debug";
    }

    // Important: do *after* initializing the configuration name
    data.RootDirectory = System.IO.Path.GetFullPath(".");
    data.OutputRootDirectory = System.IO.Path.GetFullPath(buildContext.BuildServer.GetVariable("OutputRootDirectory", string.Format("./output/{0}", data.Solution.ConfigurationName), showValue: true));

    data.SourceLink = new SourceLinkContext(data)
    {
        IsDisabled = buildContext.BuildServer.GetVariableAsBool("SourceLinkDisabled", false, showValue: true)
    };

    data.CodeSign = new CodeSignContext(data)
    {
        WildCard = buildContext.BuildServer.GetVariable("CodeSignWildcard", showValue: true),
        CertificateSubjectName = buildContext.BuildServer.GetVariable("CodeSignCertificateSubjectName", showValue: true),
        TimeStampUri = buildContext.BuildServer.GetVariable("CodeSignTimeStampUri", "http://timestamp.digicert.com", showValue: true)
    };

    data.Repository = new RepositoryContext(data)
    {
        Url = buildContext.BuildServer.GetVariable("RepositoryUrl", showValue: true),
        BranchName = buildContext.BuildServer.GetVariable("RepositoryBranchName", showValue: true),
        CommitId = buildContext.BuildServer.GetVariable("RepositoryCommitId", showValue: true),
        Username = buildContext.BuildServer.GetVariable("RepositoryUsername", showValue: false),
        Password = buildContext.BuildServer.GetVariable("RepositoryPassword", showValue: false)
    };

    data.SonarQube = new SonarQubeContext(data)
    {
        IsDisabled = buildContext.BuildServer.GetVariableAsBool("SonarDisabled", false, showValue: true),
        SupportBranches = buildContext.BuildServer.GetVariableAsBool("SonarSupportBranches", true, showValue: true),
        Url = buildContext.BuildServer.GetVariable("SonarUrl", showValue: true),
        Organization = buildContext.BuildServer.GetVariable("SonarOrganization", showValue: true),
        Username = buildContext.BuildServer.GetVariable("SonarUsername", showValue: false),
        Password = buildContext.BuildServer.GetVariable("SonarPassword", showValue: false),
        Project = buildContext.BuildServer.GetVariable("SonarProject", data.Solution.Name, showValue: true)
    };

    data.Includes = SplitCommaSeparatedList(buildContext.BuildServer.GetVariable("Include", string.Empty, showValue: true));
    data.Excludes = SplitCommaSeparatedList(buildContext.BuildServer.GetVariable("Exclude", string.Empty, showValue: true));

    // Specific overrides, done when we have *all* info
    parentBuildContext.CakeContext.Information("Ensuring correct runtime data based on version");

    var versionContext = data.Version;
    if (string.IsNullOrWhiteSpace(versionContext.NuGet) || versionContext.NuGet == "unknown")
    {
        parentBuildContext.CakeContext.Information("No version info specified, falling back to GitVersion");

        var gitVersion = versionContext.GetGitVersionContext(data);
        
        versionContext.MajorMinorPatch = gitVersion.MajorMinorPatch;
        versionContext.FullSemVer = gitVersion.FullSemVer;
        versionContext.NuGet = gitVersion.NuGetVersionV2;
        versionContext.CommitsSinceVersionSource = (gitVersion.CommitsSinceVersionSource ?? 0).ToString();
    }    

    parentBuildContext.CakeContext.Information("Defined version: '{0}', commits since version source: '{1}'", versionContext.FullSemVer, versionContext.CommitsSinceVersionSource);

    if (string.IsNullOrWhiteSpace(data.Repository.CommitId))
    {
        parentBuildContext.CakeContext.Information("No commit id specified, falling back to GitVersion");

        var gitVersion = versionContext.GetGitVersionContext(data);
        
        data.Repository.BranchName = gitVersion.BranchName;
        data.Repository.CommitId = gitVersion.Sha;
    }

    if (string.IsNullOrWhiteSpace(data.Repository.BranchName))
    {
        parentBuildContext.CakeContext.Information("No branch name specified, falling back to GitVersion");

        var gitVersion = versionContext.GetGitVersionContext(data);
        
        data.Repository.BranchName = gitVersion.BranchName;
    }

    var versionToCheck = versionContext.FullSemVer;
    if (versionToCheck.Contains("alpha"))
    {
        data.IsAlphaBuild = true;
    }
    else if (versionToCheck.Contains("beta"))
    {
        data.IsBetaBuild = true;
    }
    else
    {
        data.IsOfficialBuild = true;
    }

    return data;
}

//-------------------------------------------------------------

private static string DetermineChannel(GeneralContext context)
{
    var version = context.Version.FullSemVer;

    var channel = "stable";

    if (context.IsAlphaBuild)
    {
        channel = "alpha";
    }
    else if (context.IsBetaBuild)
    {
        channel = "beta";
    }

    return channel;
}

//-------------------------------------------------------------

private static string DeterminePublishType(GeneralContext context)
{
    var publishType = "Unknown";

    if (context.IsOfficialBuild)
    {
        publishType = "Official";
    }
    else if (context.IsBetaBuild)
    {
        publishType = "Beta";
    }
    else if (context.IsAlphaBuild)
    {
        publishType = "Alpha";
    }
    
    return publishType;
}