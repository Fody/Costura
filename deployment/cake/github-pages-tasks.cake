#l "github-pages-variables.cake"

#addin "nuget:?package=Cake.Git&version=0.19.0"

//-------------------------------------------------------------

public class GitHubPagesProcessor : ProcessorBase
{
    public GitHubPagesProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }    
    
    public override bool HasItems()
    {
        return BuildContext.GitHubPages.Items.Count > 0;
    }

    private string GetGitHubPagesRepositoryUrl(string projectName)
    {
        // Allow per project overrides via "GitHubPagesRepositoryUrlFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "GitHubPagesRepositoryUrlFor", BuildContext.GitHubPages.RepositoryUrl);
    }

    private string GetGitHubPagesBranchName(string projectName)
    {
        // Allow per project overrides via "GitHubPagesBranchNameFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "GitHubPagesBranchNameFor", BuildContext.GitHubPages.BranchName);
    }

    private string GetGitHubPagesEmail(string projectName)
    {
        // Allow per project overrides via "GitHubPagesEmailFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "GitHubPagesEmailFor", BuildContext.GitHubPages.Email);
    }

    private string GetGitHubPagesUserName(string projectName)
    {
        // Allow per project overrides via "GitHubPagesUserNameFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "GitHubPagesUserNameFor", BuildContext.GitHubPages.UserName);
    }

    private string GetGitHubPagesApiToken(string projectName)
    {
        // Allow per project overrides via "GitHubPagesApiTokenFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "GitHubPagesApiTokenFor", BuildContext.GitHubPages.ApiToken);
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var gitHubPage in BuildContext.GitHubPages.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, gitHubPage))
            {
                BuildContext.GitHubPages.Items.Remove(gitHubPage);
            }
        }        
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var gitHubPage in BuildContext.GitHubPages.Items)
        {
            CakeContext.Information("Updating version for GitHub page '{0}'", gitHubPage);

            var projectFileName = GetProjectFileName(BuildContext, gitHubPage);

            CakeContext.TransformConfig(projectFileName, new TransformationCollection 
            {
                { "Project/PropertyGroup/PackageVersion", BuildContext.General.Version.NuGet }
            });
        }        
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var gitHubPage in BuildContext.GitHubPages.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building GitHub page '{0}'", gitHubPage);

            var projectFileName = GetProjectFileName(BuildContext, gitHubPage);
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, gitHubPage);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = string.Format("{0}/{1}/", BuildContext.General.OutputRootDirectory, gitHubPage);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            CakeContext.MSBuild(projectFileName, msBuildSettings);
        }        
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var gitHubPage in BuildContext.GitHubPages.Items)
        {
            BuildContext.CakeContext.LogSeparator("Packaging GitHub pages '{0}'", gitHubPage);

            var projectFileName = string.Format("./src/{0}/{0}.csproj", gitHubPage);

            var outputDirectory = string.Format("{0}/{1}/", BuildContext.General.OutputRootDirectory, gitHubPage);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);

            CakeContext.Information("1) Using 'dotnet publish' to package '{0}'", gitHubPage);

            var msBuildSettings = new DotNetCoreMSBuildSettings();

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", outputDirectory);
            msBuildSettings.WithProperty("ConfigurationName", BuildContext.General.Solution.ConfigurationName);
            msBuildSettings.WithProperty("PackageVersion", BuildContext.General.Version.NuGet);

            var publishSettings = new DotNetCorePublishSettings
            {
                MSBuildSettings = msBuildSettings,
                OutputDirectory = outputDirectory,
                Configuration = BuildContext.General.Solution.ConfigurationName
            };

            CakeContext.DotNetCorePublish(projectFileName, publishSettings);
        }        
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        foreach (var gitHubPage in BuildContext.GitHubPages.Items)
        {
            if (!ShouldDeployProject(BuildContext, gitHubPage))
            {
                CakeContext.Information("GitHub page '{0}' should not be deployed", gitHubPage);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying GitHub page '{0}'", gitHubPage);

            CakeContext.Warning("Only Blazor apps are supported as GitHub pages");

            var temporaryDirectory = GetTempDirectory(BuildContext, "gh-pages", gitHubPage);

            CakeContext.CleanDirectory(temporaryDirectory);

            var repositoryUrl = GetGitHubPagesRepositoryUrl(gitHubPage);
            var branchName = GetGitHubPagesBranchName(gitHubPage);
            var email = GetGitHubPagesEmail(gitHubPage);
            var userName = GetGitHubPagesUserName(gitHubPage);
            var apiToken = GetGitHubPagesApiToken(gitHubPage);

            CakeContext.Information("1) Cloning repository '{0}' using branch name '{1}'", repositoryUrl, branchName);

            CakeContext.GitClone(repositoryUrl, temporaryDirectory, userName, apiToken, new GitCloneSettings
            {
                BranchName = branchName
            });

            CakeContext.Information("2) Updating the GitHub pages branch with latest source");

            // Special directory we need to distribute (e.g. output\Release\Blazorc.PatternFly.Example\Blazorc.PatternFly.Example\dist)
            var sourceDirectory = string.Format("{0}/{1}/{1}/dist", BuildContext.General.OutputRootDirectory, gitHubPage);
            var sourcePattern = string.Format("{0}/**/*", sourceDirectory);

            CakeContext.Debug("Copying all files from '{0}' => '{1}'", sourcePattern, temporaryDirectory);

            CakeContext.CopyFiles(sourcePattern, temporaryDirectory, true);

            CakeContext.Information("3) Committing latest GitHub pages");

            CakeContext.GitAddAll(temporaryDirectory);
            CakeContext.GitCommit(temporaryDirectory, "Build server", email, string.Format("Auto-update GitHub pages: '{0}'", BuildContext.General.Version.NuGet));

            CakeContext.Information("4) Pushing code back to repository '{0}'", repositoryUrl);

            CakeContext.GitPush(temporaryDirectory, userName, apiToken);

            await BuildContext.Notifications.NotifyAsync(gitHubPage, string.Format("Deployed to GitHub pages"), TargetType.GitHubPages);
        }        
    }

    public override async Task FinalizeAsync()
    {

    }
}
