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
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var dependency in BuildContext.Dependencies.Items.ToList())
        {
            // Note: dependencies should always be built
            // if (!ShouldProcessProject(BuildContext, dependency))
            // {
            //     BuildContext.Dependencies.Items.Remove(dependency);
            // }
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

            ConfigureMsBuild(BuildContext, msBuildSettings, dependency);
            
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

            var outputDirectory = GetProjectOutputDirectory(BuildContext, dependency);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            // SourceLink specific stuff
            if (IsSourceLinkSupported(BuildContext, projectFileName))
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

                InjectSourceLinkInProjectFile(BuildContext, projectFileName);
            }

            RunMsBuild(BuildContext, dependency, projectFileName, msBuildSettings);
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