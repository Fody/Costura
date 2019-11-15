#l "components-variables.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"

using System.Xml.Linq;

//-------------------------------------------------------------

public class ComponentsProcessor : ProcessorBase
{
    public ComponentsProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }

    public override bool HasItems()
    {
        return BuildContext.Components.Items.Count > 0;
    }

    private string GetComponentNuGetRepositoryUrl(string projectName)
    {
        // Allow per project overrides via "NuGetRepositoryUrlFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "NuGetRepositoryUrlFor", BuildContext.Components.NuGetRepositoryUrl);
    }

    private string GetComponentNuGetRepositoryApiKey(string projectName)
    {
        // Allow per project overrides via "NuGetRepositoryApiKeyFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "NuGetRepositoryApiKeyFor", BuildContext.Components.NuGetRepositoryApiKey);
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var component in BuildContext.Components.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, component))
            {
                BuildContext.Components.Items.Remove(component);
            }
        }

        if (BuildContext.General.IsLocalBuild && BuildContext.General.Target.ToLower().Contains("packagelocal"))
        {
            foreach (var component in BuildContext.Components.Items)
            {
                var cacheDirectory = Environment.ExpandEnvironmentVariables(string.Format("%userprofile%/.nuget/packages/{0}/{1}", component, BuildContext.General.Version.NuGet));

                CakeContext.Information("Checking for existing local NuGet cached version at '{0}'", cacheDirectory);

                var retryCount = 3;

                while (retryCount > 0)
                {
                    if (!CakeContext.DirectoryExists(cacheDirectory))
                    {
                        break;
                    }

                    CakeContext.Information("Deleting already existing NuGet cached version from '{0}'", cacheDirectory);
                    
                    CakeContext.DeleteDirectory(cacheDirectory, new DeleteDirectorySettings
                    {
                        Force = true,
                        Recursive = true
                    });

                    await System.Threading.Tasks.Task.Delay(1000);

                    retryCount--;
                }            
            }
        }        
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var component in BuildContext.Components.Items)
        {
            CakeContext.Information("Updating version for component '{0}'", component);

            var projectFileName = GetProjectFileName(BuildContext, component);

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
        
        foreach (var component in BuildContext.Components.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building component '{0}'", component);

            var projectFileName = GetProjectFileName(BuildContext, component);
            
            var msBuildSettings = new MSBuildSettings 
            {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, component);
            
            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(BuildContext, component);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            // SourceLink specific stuff
            if (IsSourceLinkSupported(BuildContext, projectFileName))
            {
                var repositoryUrl = BuildContext.General.Repository.Url;
                var repositoryCommitId = BuildContext.General.Repository.CommitId;
                if (!BuildContext.General.SourceLink.IsDisabled && 
                    !BuildContext.General.IsLocalBuild && 
                    !string.IsNullOrWhiteSpace(repositoryUrl))
                {       
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
            }

            CakeContext.MSBuild(projectFileName, msBuildSettings);
        }        
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }

        var configurationName = BuildContext.General.Solution.ConfigurationName;

        foreach (var component in BuildContext.Components.Items)
        {
            BuildContext.CakeContext.LogSeparator("Packaging component '{0}'", component);

            var projectDirectory = string.Format("./src/{0}", component);
            var projectFileName = string.Format("{0}/{1}.csproj", projectDirectory, component);
            var outputDirectory = GetProjectOutputDirectory(BuildContext, component);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);

            // Step 1: remove intermediate files to ensure we have the same results on the build server, somehow NuGet 
            // targets tries to find the resource assemblies in [ProjectName]\obj\Release\net46\de\[ProjectName].resources.dll',
            // we won't run a clean on the project since it will clean out the actual output (which we still need for packaging)

            CakeContext.Information("Cleaning intermediate files for component '{0}'", component);

            var binFolderPattern = string.Format("{0}/bin/{1}/**.dll", projectDirectory, configurationName);

            CakeContext.Information("Deleting 'bin' directory contents using '{0}'", binFolderPattern);

            var binFiles = CakeContext.GetFiles(binFolderPattern);
            CakeContext.DeleteFiles(binFiles);

            var objFolderPattern = string.Format("{0}/obj/{1}/**.dll", projectDirectory, configurationName);

            CakeContext.Information("Deleting 'bin' directory contents using '{0}'", objFolderPattern);

            var objFiles = CakeContext.GetFiles(objFolderPattern);
            CakeContext.DeleteFiles(objFiles);

            CakeContext.Information(string.Empty);

            // Step 2: Go packaging!
            CakeContext.Information("Using 'msbuild' to package '{0}'", component);

            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = configurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, component, "pack");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("ConfigurationName", configurationName);
            msBuildSettings.WithProperty("PackageVersion", BuildContext.General.Version.NuGet);

            // SourceLink specific stuff
            var repositoryUrl = BuildContext.General.Repository.Url;
            var repositoryCommitId = BuildContext.General.Repository.CommitId;
            if (!BuildContext.General.SourceLink.IsDisabled && 
                !BuildContext.General.IsLocalBuild && 
                !string.IsNullOrWhiteSpace(repositoryUrl))
            {       
                CakeContext.Information("Repository url is specified, adding commit specific data to package");

                // TODO: For now we are assuming everything is git, we might need to change that in the future
                // See why we set the values at https://github.com/dotnet/sourcelink/issues/159#issuecomment-427639278
                msBuildSettings.WithProperty("PublishRepositoryUrl", "true");
                msBuildSettings.WithProperty("RepositoryType", "git");
                msBuildSettings.WithProperty("RepositoryUrl", repositoryUrl);
                msBuildSettings.WithProperty("RevisionId", repositoryCommitId);
            }
            
            // Fix for .NET Core 3.0, see https://github.com/dotnet/core-sdk/issues/192, it
            // uses obj/release instead of [outputdirectory]
            msBuildSettings.WithProperty("DotNetPackIntermediateOutputPath", outputDirectory);
            
            msBuildSettings.WithProperty("NoBuild", "true");
            msBuildSettings.Targets.Add("Pack");

            CakeContext.MSBuild(projectFileName, msBuildSettings);

            BuildContext.CakeContext.LogSeparator();
        }

        var codeSign = (!BuildContext.General.IsCiBuild && 
                        !BuildContext.General.IsLocalBuild && 
                        !string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName));
        if (codeSign)
        {
            // For details, see https://docs.microsoft.com/en-us/nuget/create-packages/sign-a-package
            // nuget sign MyPackage.nupkg -CertificateSubjectName <MyCertSubjectName> -Timestamper <TimestampServiceURL>
            var filesToSign = CakeContext.GetFiles(string.Format("{0}/*.nupkg", BuildContext.General.OutputRootDirectory));

            foreach (var fileToSign in filesToSign)
            {
                CakeContext.Information("Signing NuGet package '{0}' using certificate subject '{1}'", fileToSign, BuildContext.General.CodeSign.CertificateSubjectName);

                var exitCode = CakeContext.StartProcess(BuildContext.General.NuGet.Executable, new ProcessSettings
                {
                    Arguments = string.Format("sign \"{0}\" -CertificateSubjectName \"{1}\" -Timestamper \"{2}\"", 
                        fileToSign, BuildContext.General.CodeSign.CertificateSubjectName, BuildContext.General.CodeSign.TimeStampUri)
                });

                CakeContext.Information("Signing NuGet package exited with '{0}'", exitCode);
            }
        }        
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var component in BuildContext.Components.Items)
        {
            if (!ShouldDeployProject(BuildContext, component))
            {
                CakeContext.Information("Component '{0}' should not be deployed", component);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying component '{0}'", component);

            var packageToPush = string.Format("{0}/{1}.{2}.nupkg", BuildContext.General.OutputRootDirectory, component, BuildContext.General.Version.NuGet);
            var nuGetRepositoryUrl = GetComponentNuGetRepositoryUrl(component);
            var nuGetRepositoryApiKey = GetComponentNuGetRepositoryApiKey(component);

            if (string.IsNullOrWhiteSpace(nuGetRepositoryUrl))
            {
                throw new Exception("NuGet repository is empty, as a protection mechanism this must *always* be specified to make sure packages aren't accidentally deployed to the default public NuGet feed");
            }

            CakeContext.NuGetPush(packageToPush, new NuGetPushSettings
            {
                Source = nuGetRepositoryUrl,
                ApiKey = nuGetRepositoryApiKey,
                ArgumentCustomization = args => args.Append("-SkipDuplicate")
            });

            await BuildContext.Notifications.NotifyAsync(component, string.Format("Deployed to NuGet store"), TargetType.Component);
        }        
    }

    public override async Task FinalizeAsync()
    {

    }
}