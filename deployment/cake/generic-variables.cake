#l "buildserver.cake"

using System.Globalization;
using System.Text.Json.Serialization;

//-------------------------------------------------------------

public class GeneralContext : BuildContextWithItemsBase
{
    public GeneralContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
        SkipComponentsThatAreNotDeployable = true;
        EnableMsBuildBinaryLog = false;
        EnableMsBuildFileLog = false;
        EnableMsBuildXmlLog = false;
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

    public bool EnableMsBuildBinaryLog { get; set; }
    public bool EnableMsBuildFileLog { get; set; }
    public bool EnableMsBuildXmlLog { get; set; }

    public VersionContext Version { get; set; }
    public CopyrightContext Copyright { get; set; }
    public NuGetContext NuGet { get; set; }
    public SolutionContext Solution { get; set; }
    public SourceLinkContext SourceLink { get; set; }
    public CodeSignContext CodeSign { get; set; }
    public AzureCodeSignContext AzureCodeSign { get; set; }
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

// Running manually since v7

public class GitVersionContext
{
    [JsonPropertyName("AssemblySemFileVer")]
    public string AssemblySemFileVer { get; set; }

    [JsonPropertyName("AssemblySemVer")]
    public string AssemblySemVer { get; set; }

    [JsonPropertyName("BranchName")]
    public string BranchName { get; set; }

    [JsonPropertyName("BuildMetaData")]
    public int? BuildMetaData { get; set; }

    [JsonPropertyName("CommitCountSourceDistance")]
    public int? CommitCountSourceDistance { get; set; }

    [JsonPropertyName("CommitCountSourceSha")]
    public string CommitCountSourceSha { get; set; }

    [JsonPropertyName("CommitDate")]
    public string CommitDate { get; set; }

    [JsonPropertyName("CommitsSinceVersionSource")]
    public int? CommitsSinceVersionSource { get; set; }

    [JsonPropertyName("CustomVersion")]
    public string CustomVersion { get; set; }

    [JsonPropertyName("EscapedBranchName")]
    public string EscapedBranchName { get; set; }

    [JsonPropertyName("FullBuildMetaData")]
    public string FullBuildMetaData { get; set; }

    [JsonPropertyName("FullSemVer")]
    public string FullSemVer { get; set; }

    [JsonPropertyName("InformationalVersion")]
    public string InformationalVersion { get; set; }

    [JsonPropertyName("Major")]
    public int Major { get; set; }

    [JsonPropertyName("MajorMinorPatch")]
    public string MajorMinorPatch { get; set; }

    [JsonPropertyName("Minor")]
    public int Minor { get; set; }

    [JsonPropertyName("Patch")]
    public int Patch { get; set; }

    [JsonPropertyName("PreReleaseLabel")]
    public string PreReleaseLabel { get; set; }

    [JsonPropertyName("PreReleaseLabelName")]
    public string PreReleaseLabelName { get; set; }

    [JsonPropertyName("PreReleaseLabelNameWithDash")]
    public string PreReleaseLabelNameWithDash { get; set; }

    [JsonPropertyName("PreReleaseLabelWithDash")]
    public string PreReleaseLabelWithDash { get; set; }

    [JsonPropertyName("PreReleaseNumber")]
    public int? PreReleaseNumber { get; set; }

    [JsonPropertyName("SemVer")]
    public string SemVer { get; set; }

    [JsonPropertyName("SemVerSourceIncrement")]
    public string SemVerSourceIncrement { get; set; }

    [JsonPropertyName("SemVerSourceSemVer")]
    public string SemVerSourceSemVer { get; set; }

    [JsonPropertyName("SemVerSourceSha")]
    public string SemVerSourceSha { get; set; }

    [JsonPropertyName("Sha")]
    public string Sha { get; set; }

    [JsonPropertyName("ShortSha")]
    public string ShortSha { get; set; }

    [JsonPropertyName("UncommittedChanges")]
    public int? UncommittedChanges { get; set; }

    [JsonPropertyName("VersionSourceDistance")]
    public int? VersionSourceDistance { get; set; }

    [JsonPropertyName("VersionSourceIncrement")]
    public string VersionSourceIncrement { get; set; }

    [JsonPropertyName("VersionSourceSemVer")]
    public string VersionSourceSemVer { get; set; }

    [JsonPropertyName("VersionSourceSha")]
    public string VersionSourceSha { get; set; }

    [JsonPropertyName("WeightedPreReleaseNumber")]
    public int? WeightedPreReleaseNumber { get; set; }

    // Legacy properties
    public string? BuildMetaDataPadded { get; set; }
    public string? LegacySemVerPadded { get; set; }
    public string? NuGetVersionV2 { get; set; }
}

//-------------------------------------------------------------

public class VersionContext : BuildContextBase
{
    private GitVersionContext _gitVersionContext;

    public VersionContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public GitVersionContext GetGitVersionContext(GeneralContext generalContext)
    {
        if (_gitVersionContext is null)
        {
            CakeContext.Information("[{0}] Running GitVersion", GetTime());

            // Install GitVersion tool just before running it
            CakeContext.Information("Installing GitVersion.Tool package");

            CakeContext.InstallTool(BuildDependencies.GitVersionTool);
        
            // Running manual since v7                
            var gitVersion = CakeContext.Tools.Resolve(new [] { "GitVersion.exe", "dotnet-gitversion", "dotnet-gitversion.exe" });

            var gitVersionArgumentsBuilder = new ProcessArgumentBuilder();
            gitVersionArgumentsBuilder.Append("--output json");
            gitVersionArgumentsBuilder.Append("--verbosity normal");

            var gitVersionProcessSettings = new ProcessSettings
            {
                Arguments = gitVersionArgumentsBuilder,
                Silent = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var checkProcess = CakeContext.StartAndReturnProcess(gitVersion, gitVersionProcessSettings))
            {
                checkProcess.WaitForExit();

                var output = string.Join('\n', checkProcess.GetStandardOutput());

                CakeContext.Information(output);

                var errors = Regex.Matches(output, @"( *ERROR:? [^\n]*)\n([^\n]*)").Cast<Match>()
                    .SelectMany(match => new[] { match.Groups[1].Value, match.Groups[2].Value });
                foreach (var error in errors)
                {
                    CakeContext.Error(error);
                }

                var version = JsonSerializer.Deserialize<GitVersionContext>(output);

                // "Legacy" properties
                version.BuildMetaDataPadded = version.CommitCountSourceDistance.HasValue
                    ? version.CommitCountSourceDistance.Value.ToString("D4", CultureInfo.InvariantCulture)
                    : (version.BuildMetaData?.ToString("D4", CultureInfo.InvariantCulture) ?? string.Empty);

                version.LegacySemVerPadded = string.IsNullOrEmpty(version.BuildMetaDataPadded) 
                    ? version.MajorMinorPatch 
                    : version.MajorMinorPatch + version.PreReleaseLabelNameWithDash + version.BuildMetaDataPadded;

                version.NuGetVersionV2 = version.LegacySemVerPadded.ToLower();

                _gitVersionContext = version;
            }

            CakeContext.Information("[{0}] Finished GitVersion", GetTime());
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
    public string VersionSourceDistance  { get; set; }

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
    public string PackageSourcesNames { get; set; }
    public string PackageSourcesTokens { get; set; }
    public string PackageSourcesUsernames { get; set; }
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
        CakeContext.Information($"NuGet executable path '{Executable}'");
        CakeContext.Information($"NuGet executable version '{FileVersionInfo.GetVersionInfo(Executable).FileVersion}'");
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
        CakeContext.Information($"Solution filename: '{FileName}'");
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
    public string HashAlgorithm { get; set; }

    public bool IsAvailable
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CertificateSubjectName))
            {
                return false;
            }

            return true;
        }
    }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
        if (!IsAvailable)
        {
            CakeContext.Information($"Code signing is not configured");
            return;
        }

        CakeContext.Information($"Code signing subject name: '{CertificateSubjectName}'");
        CakeContext.Information($"Code signing timestamp uri: '{TimeStampUri}'");
        CakeContext.Information($"Code signing hash algorithm: '{HashAlgorithm}'");
    }
}

//-------------------------------------------------------------

public class AzureCodeSignContext : BuildContextBase
{
    public AzureCodeSignContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string VaultName { get; set; }
    public string VaultUrl { get { return $"https://{VaultName}.vault.azure.net"; } }
    public string CertificateName { get; set; }
    public string TimeStampUri { get; set; }
    public string HashAlgorithm { get; set; }
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    public bool IsAvailable
    {
        get
        {
            if (string.IsNullOrWhiteSpace(VaultName) ||
                string.IsNullOrWhiteSpace(CertificateName) ||
                string.IsNullOrWhiteSpace(TenantId) ||
                string.IsNullOrWhiteSpace(ClientId) ||
                string.IsNullOrWhiteSpace(ClientSecret))
            {
                return false;
            }

            return true;
        }
    }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
        if (!IsAvailable)
        {
            CakeContext.Information($"Azure Code signing is not configured");
            return;
        }

        CakeContext.Information($"Azure Code vault name: '{VaultName}'");
        CakeContext.Information($"Azure Code vault URL: '{VaultUrl}'");
        CakeContext.Information($"Azure Code signing certificate name: '{CertificateName}'");
        CakeContext.Information($"Azure Code signing timestamp uri: '{TimeStampUri}'");
        CakeContext.Information($"Azure Code signing hash algorithm: '{HashAlgorithm}'");
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
    public string Token { get; set; }
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
        VersionSourceDistance  = buildContext.BuildServer.GetVariable("GitVersion_VersionSourceDistance", "unknown", showValue: true)
    };

    data.Copyright = new CopyrightContext(data)
    {
        Company = buildContext.BuildServer.GetVariable("Company", showValue: true),
        StartYear = buildContext.BuildServer.GetVariable("StartYear", showValue: true)
    };

    data.NuGet = new NuGetContext(data)
    {
        PackageSources = buildContext.BuildServer.GetVariable("NuGetPackageSources", showValue: true),
        PackageSourcesNames = buildContext.BuildServer.GetVariable("NuGetPackageSourcesNames", showValue: true),
        PackageSourcesUsernames = buildContext.BuildServer.GetVariable("NuGetPackageSourcesUsernames", showValue: false),
        PackageSourcesTokens = buildContext.BuildServer.GetVariable("NuGetPackageSourcesTokens", showValue: false),
        // Executable = "./tools/nuget.exe",
        Executable = buildContext.CakeContext.Tools.Resolve("nuget.exe").FullPath,
        LocalPackagesDirectory = "c:\\source\\_packages",
        RestoreUsingNuGet = buildContext.BuildServer.GetVariableAsBool("NuGet_RestoreUsingNuGet", false, showValue: true),
        RestoreUsingDotNetRestore = buildContext.BuildServer.GetVariableAsBool("NuGet_RestoreUsingDotNetRestore", true, showValue: true),
        NoDependencies = buildContext.BuildServer.GetVariableAsBool("NuGet_NoDependencies", true, showValue: true)
    };

    var solutionName = buildContext.BuildServer.GetVariable("SolutionName", showValue: true);

    var solutionExtension = "slnx";

    var solutionFiles = GetFiles(string.Format("./src/{0}.{1}", solutionName, solutionExtension));
    if (solutionFiles.Count == 0)
    {
        solutionExtension = "sln";
    }
    
    data.Solution = new SolutionContext(data)
    {
        Name = solutionName,
        AssemblyInfoFileName = "./src/SolutionAssemblyInfo.cs",
        FileName = string.Format("./src/{0}", string.Format("{0}.{1}", solutionName, solutionExtension)),
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

    data.EnableMsBuildBinaryLog = buildContext.BuildServer.GetVariableAsBool("EnableMsBuildBinaryLog", false, showValue: true);
    data.EnableMsBuildFileLog = buildContext.BuildServer.GetVariableAsBool("EnableMsBuildFileLog", false, showValue: true);
    data.EnableMsBuildXmlLog = buildContext.BuildServer.GetVariableAsBool("EnableMsBuildXmlLog", false, showValue: true);

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
        TimeStampUri = buildContext.BuildServer.GetVariable("CodeSignTimeStampUri", "http://timestamp.digicert.com", showValue: true),
        HashAlgorithm = buildContext.BuildServer.GetVariable("CodeSignHashAlgorithm", "SHA256", showValue: true)
    };

    data.AzureCodeSign = new AzureCodeSignContext(data)
    {
        VaultName = buildContext.BuildServer.GetVariable("AzureCodeSignVaultName", showValue: true),
        CertificateName = buildContext.BuildServer.GetVariable("AzureCodeSignCertificateName", showValue: true),
        TimeStampUri = buildContext.BuildServer.GetVariable("AzureCodeSignTimeStampUri", "http://timestamp.digicert.com", showValue: true),
        HashAlgorithm = buildContext.BuildServer.GetVariable("AzureCodeSignHashAlgorithm", "SHA256", showValue: true),
        TenantId = buildContext.BuildServer.GetVariable("AzureCodeSignTenantId", showValue: false),
        ClientId = buildContext.BuildServer.GetVariable("AzureCodeSignClientId", showValue: false),
        ClientSecret = buildContext.BuildServer.GetVariable("AzureCodeSignClientSecret", showValue: false),
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
        Token = buildContext.BuildServer.GetVariable("SonarToken", showValue: false),
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
        versionContext.VersionSourceDistance = (gitVersion.VersionSourceDistance ?? 0).ToString();
    }    

    parentBuildContext.CakeContext.Information("Full sem ver: '{0}'", versionContext.FullSemVer);
    parentBuildContext.CakeContext.Information("Version source distance: '{0}'", versionContext.VersionSourceDistance);
    parentBuildContext.CakeContext.Information("NuGet version: '{0}'", versionContext.NuGet);

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
