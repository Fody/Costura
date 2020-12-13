#pragma warning disable 1998

#l "docker-variables.cake"
#l "lib-octopusdeploy.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"
#addin "nuget:?package=Cake.Docker&version=0.11.1"

//-------------------------------------------------------------

public class DockerImagesProcessor : ProcessorBase
{
    public DockerImagesProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }

    public override bool HasItems()
    {
        return BuildContext.DockerImages.Items.Count > 0;
    }

    private string GetDockerRegistryUrl(string projectName)
    {
        // Allow per project overrides via "DockerRegistryUrlFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "DockerRegistryUrlFor", BuildContext.DockerImages.DockerRegistryUrl);
    }

    private string GetDockerRegistryUserName(string projectName)
    {
        // Allow per project overrides via "DockerRegistryUserNameFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "DockerRegistryUserNameFor", BuildContext.DockerImages.DockerRegistryUserName);
    }

    private string GetDockerRegistryPassword(string projectName)
    {
        // Allow per project overrides via "DockerRegistryPasswordFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "DockerRegistryPasswordFor", BuildContext.DockerImages.DockerRegistryPassword);
    }

    private string GetDockerImageName(string projectName)
    {
        var name = projectName.Replace(".", "-");
        return name.ToLower();
    }

    private string GetDockerImageTag(string projectName, string version)
    {
        var dockerRegistryUrl = GetDockerRegistryUrl(projectName);

        var tag = string.Format("{0}/{1}:{2}", dockerRegistryUrl, GetDockerImageName(projectName), version);
        return tag.ToLower();
    }

    private void ConfigureDockerSettings(AutoToolSettings dockerSettings)
    {
        var engineUrl = BuildContext.DockerImages.DockerEngineUrl;
        if (!string.IsNullOrWhiteSpace(engineUrl))
        {
            CakeContext.Information("Using remote docker engine: '{0}'", engineUrl);

            dockerSettings.ArgumentCustomization = args => args.Prepend($"-H {engineUrl}");
            //dockerSettings.BuildArg = new [] { $"DOCKER_HOST={engineUrl}" };
        }
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var dockerImage in BuildContext.DockerImages.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, dockerImage))
            {
                BuildContext.DockerImages.Items.Remove(dockerImage);
            }
        }        
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Doesn't seem neccessary yet
        // foreach (var dockerImage in BuildContext.DockerImages.Items)
        // {
        //     Information("Updating version for docker image '{0}'", dockerImage);

        //     var projectFileName = GetProjectFileName(BuildContext, dockerImage);

        //     TransformConfig(projectFileName, new TransformationCollection 
        //     {
        //         { "Project/PropertyGroup/PackageVersion", VersionNuGet }
        //     });
        // }        
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        foreach (var dockerImage in BuildContext.DockerImages.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building docker image '{0}'", dockerImage);

            var projectFileName = GetProjectFileName(BuildContext, dockerImage);
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, dockerImage);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(BuildContext, dockerImage);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            RunMsBuild(BuildContext, dockerImage, projectFileName, msBuildSettings);
        }        
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // The following directories are being created, ready for docker images to be used:
        // ./output => output of the publish step
        // ./config => docker image and config files, in case they need to be packed as well

        foreach (var dockerImage in BuildContext.DockerImages.Items)
        {
            if (!ShouldDeployProject(BuildContext, dockerImage))
            {
                CakeContext.Information("Docker image '{0}' should not be deployed", dockerImage);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Packaging docker image '{0}'", dockerImage);

            var projectFileName = GetProjectFileName(BuildContext, dockerImage);
            var dockerImageSpecificationDirectory = System.IO.Path.Combine(".", "deployment", "docker", dockerImage);
            var dockerImageSpecificationFileName = System.IO.Path.Combine(dockerImageSpecificationDirectory, dockerImage);

            var outputRootDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, dockerImage, "output");

            CakeContext.Information("1) Preparing ./config for package '{0}'", dockerImage);

            // ./config
            var confTargetDirectory = System.IO.Path.Combine(outputRootDirectory, "conf");
            CakeContext.Information("Conf directory: '{0}'", confTargetDirectory);

            CakeContext.CreateDirectory(confTargetDirectory);

            var confSourceDirectory = string.Format("{0}/*", dockerImageSpecificationDirectory);
            CakeContext.Information("Copying files from '{0}' => '{1}'", confSourceDirectory, confTargetDirectory);

            CakeContext.CopyFiles(confSourceDirectory, confTargetDirectory, true);

            BuildContext.CakeContext.LogSeparator();

            CakeContext.Information("2) Preparing ./output using 'dotnet publish' for package '{0}'", dockerImage);

            // ./output
            var outputDirectory = System.IO.Path.Combine(outputRootDirectory, "output");
            CakeContext.Information("Output directory: '{0}'", outputDirectory);

            var msBuildSettings = new DotNetCoreMSBuildSettings();

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", outputDirectory);
            msBuildSettings.WithProperty("ConfigurationName", BuildContext.General.Solution.ConfigurationName);
            msBuildSettings.WithProperty("PackageVersion", BuildContext.General.Version.NuGet);

            // Disable code analyses, we experienced publish issues with mvc .net core projects
            msBuildSettings.WithProperty("RunCodeAnalysis", "false");

            var publishSettings = new DotNetCorePublishSettings
            {
                MSBuildSettings = msBuildSettings,
                OutputDirectory = outputDirectory,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                //NoBuild = true
            };

            CakeContext.DotNetCorePublish(projectFileName, publishSettings);

            BuildContext.CakeContext.LogSeparator();

            CakeContext.Information("3) Using 'docker build' to package '{0}'", dockerImage);

            // docker build ..\..\output\Release\platform -f .\Dockerfile

            // From the docs (https://docs.microsoft.com/en-us/azure/app-service/containers/tutorial-custom-docker-image#use-a-docker-image-from-any-private-registry-optional), 
            // we need something like this:
            // docker tag <azure-container-registry-name>.azurecr.io/mydockerimage
            var dockerRegistryUrl = GetDockerRegistryUrl(dockerImage);

            // Note: to prevent all output & source files to be copied to the docker context, we will set the
            // output directory as context (to keep the footprint as small as possible)

            var dockerSettings = new DockerImageBuildSettings
            {
                NoCache = true, // Don't use cache, always make sure to fetch the right images
                File = dockerImageSpecificationFileName,
                //Platform = "linux",
                Tag = new string[] { GetDockerImageTag(dockerImage, BuildContext.General.Version.NuGet) }
            };

            ConfigureDockerSettings(dockerSettings);

            CakeContext.Information("Docker files source directory: '{0}'", outputRootDirectory);

            CakeContext.DockerBuild(dockerSettings, outputRootDirectory);

            BuildContext.CakeContext.LogSeparator();
        }        
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var dockerImage in BuildContext.DockerImages.Items)
        {
            if (!ShouldDeployProject(BuildContext, dockerImage))
            {
                CakeContext.Information("Docker image '{0}' should not be deployed", dockerImage);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying docker image '{0}'", dockerImage);

            var dockerRegistryUrl = GetDockerRegistryUrl(dockerImage);
            var dockerRegistryUserName = GetDockerRegistryUserName(dockerImage);
            var dockerRegistryPassword = GetDockerRegistryPassword(dockerImage);
            var dockerImageName = GetDockerImageName(dockerImage);
            var dockerImageTag = GetDockerImageTag(dockerImage, BuildContext.General.Version.NuGet);
            var octopusRepositoryUrl = BuildContext.OctopusDeploy.GetRepositoryUrl(dockerImage);
            var octopusRepositoryApiKey = BuildContext.OctopusDeploy.GetRepositoryApiKey(dockerImage);
            var octopusDeploymentTarget = BuildContext.OctopusDeploy.GetDeploymentTarget(dockerImage);

            if (string.IsNullOrWhiteSpace(dockerRegistryUrl))
            {
                throw new Exception("Docker registry url is empty, as a protection mechanism this must *always* be specified to make sure packages aren't accidentally deployed to some default public registry");
            }

            // Note: we are logging in each time because the registry might be different per container
            CakeContext.Information("Logging in to docker @ '{0}'", dockerRegistryUrl);

            var dockerLoginSettings = new DockerRegistryLoginSettings
            {
                Username = dockerRegistryUserName,
                Password = dockerRegistryPassword
            };

            ConfigureDockerSettings(dockerLoginSettings);

            CakeContext.DockerLogin(dockerLoginSettings, dockerRegistryUrl);

            try
            {
                CakeContext.Information("Pushing docker images with tag '{0}' to '{1}'", dockerImageTag, dockerRegistryUrl);

                var dockerImagePushSettings = new DockerImagePushSettings
                {
                };

                ConfigureDockerSettings(dockerImagePushSettings);

                CakeContext.DockerPush(dockerImagePushSettings, dockerImageTag);

                if (string.IsNullOrWhiteSpace(octopusRepositoryUrl))
                {
                    CakeContext.Warning("Octopus Deploy url is not specified, skipping deployment to Octopus Deploy");
                    continue;
                }

                var imageVersion = BuildContext.General.Version.NuGet;

                CakeContext.Information("Creating release '{0}' in Octopus Deploy", imageVersion);

                CakeContext.OctoCreateRelease(dockerImage, new CreateReleaseSettings 
                {
                    Server = octopusRepositoryUrl,
                    ApiKey = octopusRepositoryApiKey,
                    ReleaseNumber = imageVersion,
                    DefaultPackageVersion = imageVersion,
                    IgnoreExisting = true,
                    Packages = new Dictionary<string, string>
                    {
                        { dockerImageName, imageVersion }
                    }
                });

                CakeContext.Information("Deploying release '{0}' via Octopus Deploy", imageVersion);

                CakeContext.OctoDeployRelease(octopusRepositoryUrl, octopusRepositoryApiKey, dockerImage, octopusDeploymentTarget, 
                    imageVersion, new OctopusDeployReleaseDeploymentSettings
                {
                    ShowProgress = true,
                    WaitForDeployment = true,
                    DeploymentTimeout = TimeSpan.FromMinutes(5),
                    CancelOnTimeout = true,
                    GuidedFailure = true,
                    Force = true,
                    NoRawLog = true,
                });

                await BuildContext.Notifications.NotifyAsync(dockerImage, string.Format("Deployed to Octopus Deploy"), TargetType.DockerImage);
            }
            finally
            {
                CakeContext.Information("Logging out of docker @ '{0}'", dockerRegistryUrl);

                var dockerLogoutSettings = new DockerRegistryLogoutSettings
                {
                };

                ConfigureDockerSettings(dockerLogoutSettings);

                CakeContext.DockerLogout(dockerLogoutSettings, dockerRegistryUrl);
            }
        }        
    }

    public override async Task FinalizeAsync()
    {

    }
}
