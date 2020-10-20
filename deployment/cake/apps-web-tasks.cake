#pragma warning disable 1998

#l "apps-web-variables.cake"
#l "lib-octopusdeploy.cake"

#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=Microsoft.Azure.KeyVault.Core&version=1.0.0"
#addin "nuget:?package=WindowsAzure.Storage&version=9.1.1"

//-------------------------------------------------------------

public class WebProcessor : ProcessorBase
{
    public WebProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }

    public override bool HasItems()
    {
        return BuildContext.Web.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var webApp in BuildContext.Web.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, webApp))
            {
                BuildContext.Web.Items.Remove(webApp);
            }
        }
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var webApp in BuildContext.Web.Items)
        {
            CakeContext.Information("Updating version for web app '{0}'", webApp);

            var projectFileName = GetProjectFileName(BuildContext, webApp);

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

        foreach (var webApp in BuildContext.Web.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building web app '{0}'", webApp);

            var projectFileName = GetProjectFileName(BuildContext, webApp);
            
            var msBuildSettings = new MSBuildSettings 
            {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, webApp);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, webApp);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            // TODO: Enable GitLink / SourceLink, see RepositoryUrl, RepositoryBranchName, RepositoryCommitId variables

            RunMsBuild(BuildContext, webApp, projectFileName, msBuildSettings);
        }
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // For package documentation using Octopus Deploy, see https://octopus.com/docs/deployment-examples/deploying-asp.net-core-web-applications
        
        foreach (var webApp in BuildContext.Web.Items)
        {
            if (!ShouldDeployProject(BuildContext, webApp))
            {
                CakeContext.Information("Web app '{0}' should not be deployed", webApp);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Packaging web app '{0}'", webApp);

            var projectFileName = System.IO.Path.Combine(".", "src", webApp, $"{webApp}.csproj");

            var outputDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, webApp);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);

            CakeContext.Information("1) Using 'dotnet publish' to package '{0}'", webApp);

            var msBuildSettings = new DotNetCoreMSBuildSettings();

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
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
            
            CakeContext.Information("2) Using 'octo pack' to package '{0}'", webApp);

            var toolSettings = new DotNetCoreToolSettings
            {
            };

            var octoPackCommand = string.Format("--id {0} --version {1} --basePath {0}", webApp, BuildContext.General.Version.NuGet);
            CakeContext.DotNetCoreTool(outputDirectory, "octo pack", octoPackCommand, toolSettings);
            
            BuildContext.CakeContext.LogSeparator();
        }
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var webApp in BuildContext.Web.Items)
        {
            if (!ShouldDeployProject(BuildContext, webApp))
            {
                CakeContext.Information("Web app '{0}' should not be deployed", webApp);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying web app '{0}'", webApp);

            var packageToPush = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, string.Format("{0}.{1}.nupkg", webApp, BuildContext.General.Version.NuGet));
            var octopusRepositoryUrl = BuildContext.OctopusDeploy.GetRepositoryUrl(webApp);
            var octopusRepositoryApiKey = BuildContext.OctopusDeploy.GetRepositoryApiKey(webApp);
            var octopusDeploymentTarget = BuildContext.OctopusDeploy.GetDeploymentTarget(webApp);

            CakeContext.Information("1) Pushing Octopus package");

            CakeContext.OctoPush(octopusRepositoryUrl, octopusRepositoryApiKey, packageToPush, new OctopusPushSettings
            {
                ReplaceExisting = true,
            });

            CakeContext.Information("2) Creating release '{0}' in Octopus Deploy", BuildContext.General.Version.NuGet);

            CakeContext.OctoCreateRelease(webApp, new CreateReleaseSettings 
            {
                Server = octopusRepositoryUrl,
                ApiKey = octopusRepositoryApiKey,
                ReleaseNumber = BuildContext.General.Version.NuGet,
                DefaultPackageVersion = BuildContext.General.Version.NuGet,
                IgnoreExisting = true
            });

            CakeContext.Information("3) Deploying release '{0}'", BuildContext.General.Version.NuGet);

            CakeContext.OctoDeployRelease(octopusRepositoryUrl, octopusRepositoryApiKey, webApp, octopusDeploymentTarget, 
                BuildContext.General.Version.NuGet, new OctopusDeployReleaseDeploymentSettings
            {
                ShowProgress = true,
                WaitForDeployment = true,
                DeploymentTimeout = TimeSpan.FromMinutes(5),
                CancelOnTimeout = true,
                GuidedFailure = true,
                Force = true,
                NoRawLog = true,
            });

            await BuildContext.Notifications.NotifyAsync(webApp, string.Format("Deployed to Octopus Deploy"), TargetType.WebApp);
        }
    }

    public override async Task FinalizeAsync()
    {

    }
}