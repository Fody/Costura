#l "dependencies-variables.cake"

using System.Xml.Linq;

//-------------------------------------------------------------

public class DependenciesProcessor : ProcessorBase
{
    public DependenciesProcessor(BuildContext buildContext)
        : base(buildContext)
    {

    }

    public override bool HasItems()
    {
        return BuildContext.Dependencies.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        BuildContext.CakeContext.Information($"Checking '{BuildContext.Dependencies.Items.Count}' dependencies");
        
        if (!HasItems())
        {
            return;
        }

        // We need to go through this twice because a dependency can be a dependency of a dependency
        var dependenciesToBuild = new List<string>();

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        for (int i = 0; i < 3; i++)
        {
            foreach (var dependency in BuildContext.Dependencies.Items.ToList())
            {
                if (dependenciesToBuild.Contains(dependency))
                {
                    // Already done
                    continue;
                }

                BuildContext.CakeContext.Information($"Checking dependency '{dependency}' using run {i + 1}");

                if (BuildContext.Dependencies.ShouldBuildDependency(dependency, dependenciesToBuild))
                {
                    BuildContext.CakeContext.Information($"Dependency '{dependency}' should be included");

                    dependenciesToBuild.Add(dependency);
                }
            }
        }

        // TODO: How to determine the sort order? E.g. dependencies of dependencies?

        foreach (var dependency in BuildContext.Dependencies.Items.ToList())
        {
            if (!dependenciesToBuild.Contains(dependency))
            {
                BuildContext.CakeContext.Information($"Skipping dependency '{dependency}' because no dependent projects are included");

                BuildContext.Dependencies.Dependencies.Remove(dependency);
                BuildContext.Dependencies.Items.Remove(dependency);
            }
        }
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var dependency in BuildContext.Dependencies.Items)
        {
            CakeContext.Information("Updating version for dependency '{0}'", dependency);

            var projectFileName = GetProjectFileName(BuildContext, dependency);

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
        
        foreach (var dependency in BuildContext.Dependencies.Items)
        {  
            BuildContext.CakeContext.LogSeparator("Building dependency '{0}'", dependency);

            var projectFileName = GetProjectFileName(BuildContext, dependency);
            
            var msBuildSettings = new MSBuildSettings 
            {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform,
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, dependency, "build");
            
            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var isCppProject = IsCppProject(projectFileName);
            if (isCppProject)
            {
                // Special C++ exceptions
                msBuildSettings.MSBuildPlatform = MSBuildPlatform.Automatic;
                msBuildSettings.PlatformTarget = PlatformTarget.Win32;
            }

            // SourceLink specific stuff
            if (IsSourceLinkSupported(BuildContext, dependency, projectFileName))
            {
                var repositoryUrl = BuildContext.General.Repository.Url;
                var repositoryCommitId = BuildContext.General.Repository.CommitId;
  
                CakeContext.Information("Repository url is specified, enabling SourceLink to commit '{0}/commit/{1}'", 
                    repositoryUrl, repositoryCommitId);

                // TODO: For now we are assuming everything is git, we might need to change that in the future
                // See why we set the values at https://github.com/dotnet/sourcelink/issues/159#issuecomment-427639278
                msBuildSettings.WithProperty("EnableSourceLink", "true");
                msBuildSettings.WithProperty("EnableSourceControlManagerQueries", "false");
                msBuildSettings.WithProperty("PublishRepositoryUrl", "true");
                msBuildSettings.WithProperty("RepositoryType", "git");
                msBuildSettings.WithProperty("RepositoryUrl", repositoryUrl);
                msBuildSettings.WithProperty("RevisionId", repositoryCommitId);

                InjectSourceLinkInProjectFile(BuildContext, dependency, projectFileName);
            }

            // Specific code signing, requires the following MSBuild properties:
            // * CodeSignEnabled
            // * CodeSignCommand
            //
            // This feature is built to allow projects that have post-build copy
            // steps (e.g. for assets) to be signed correctly before being embedded
            if (ShouldSignImmediately(BuildContext, dependency))
            {
                var codeSignToolFileName = FindSignToolFileName(BuildContext);
                var codeSignVerifyCommand = $"verify /pa";
                var codeSignCommand = string.Format("sign /a /t {0} /n {1}", BuildContext.General.CodeSign.TimeStampUri, 
                    BuildContext.General.CodeSign.CertificateSubjectName);

                msBuildSettings.WithProperty("CodeSignToolFileName", codeSignToolFileName);
                msBuildSettings.WithProperty("CodeSignVerifyCommand", codeSignVerifyCommand);
                msBuildSettings.WithProperty("CodeSignCommand", codeSignCommand);
                msBuildSettings.WithProperty("CodeSignEnabled", "true");
            }

            RunMsBuild(BuildContext, dependency, projectFileName, msBuildSettings, "build");
        }
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        // No packaging required for dependencies
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // No deployment required for dependencies
    }

    public override async Task FinalizeAsync()
    {

    }
}