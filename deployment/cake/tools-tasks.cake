#l "tools-variables.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"

using System.Xml.Linq;

//-------------------------------------------------------------

public class ToolsProcessor : ProcessorBase
{
    public ToolsProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }

    private void EnsureChocolateyLicenseFile(string projectName)
    {
        // Required for Chocolatey

        var projectDirectory = GetProjectDirectory(projectName);
        var outputDirectory = GetProjectOutputDirectory(BuildContext, projectName);

        // Check if it already exists
        var fileName = string.Format("{0}/LICENSE.txt", outputDirectory);
        if (!CakeContext.FileExists(fileName))
        {
            CakeContext.Information("Creating Chocolatey license file for '{0}'", projectName);

            // Option 1: Copy from root
            var sourceFile = "./LICENSE";
            if (CakeContext.FileExists(sourceFile))
            {
                CakeContext.Information("Using license file from repository");

                CakeContext.CopyFile(sourceFile, fileName);
                return;
            }

            // Option 2: use expression (PackageLicenseExpression)
            throw new Exception("Cannot find ./LICENSE, which is required for Chocolatey");
        }
    }

    private void EnsureChocolateyVerificationFile(string projectName)
    {
        // Required for Chocolatey

        var projectDirectory = GetProjectDirectory(projectName);
        var outputDirectory = GetProjectOutputDirectory(BuildContext, projectName);

        // Check if it already exists
        var fileName = string.Format("{0}/VERIFICATION.txt", outputDirectory);
        if (!CakeContext.FileExists(fileName))
        {
            CakeContext.Information("Creating Chocolatey verification file for '{0}'", projectName);
            
            System.IO.File.WriteAllText(fileName, @"VERIFICATION
    Verification is intended to assist the Chocolatey moderators and community
    in verifying that this package's contents are trustworthy.
    
    <Include details of how to verify checksum contents>
    <If software vendor, explain that here - checksum verification instructions are optional>");
        }
    }

    private string GetToolsNuGetRepositoryUrls(string projectName)
    {
        // Allow per project overrides via "NuGetRepositoryUrlFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "ToolsNuGetRepositoryUrlsFor", BuildContext.Tools.NuGetRepositoryUrls);
    }

    private string GetToolsNuGetRepositoryApiKeys(string projectName)
    {
        // Allow per project overrides via "NuGetRepositoryApiKeyFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "ToolsNuGetRepositoryApiKeysFor", BuildContext.Tools.NuGetRepositoryApiKeys);
    }

    public override bool HasItems()
    {
        return BuildContext.Tools.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var tool in BuildContext.Tools.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, tool))
            {
                BuildContext.Tools.Items.Remove(tool);
            }
        }

        if (BuildContext.General.IsLocalBuild && BuildContext.General.Target.ToLower().Contains("packagelocal"))
        {
            foreach (var tool in BuildContext.Tools.Items)
            {
                var cacheDirectory = Environment.ExpandEnvironmentVariables(string.Format("%userprofile%/.nuget/packages/{0}/{1}", 
                    tool, BuildContext.General.Version.NuGet));

                CakeContext.Information("Checking for existing local NuGet cached version at '{0}'", cacheDirectory);

                var retryCount = 3;

                while (retryCount > 0)
                {
                    if (!CakeContext.DirectoryExists(cacheDirectory))
                    {
                        break;
                    }

                    CakeContext.Information("Deleting already existing NuGet cached version from '{0}'", cacheDirectory);
                    
                    CakeContext.DeleteDirectory(cacheDirectory, new DeleteDirectorySettings()
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

        foreach (var tool in BuildContext.Tools.Items)
        {
            CakeContext.Information("Updating version for tool '{0}'", tool);

            var projectFileName = GetProjectFileName(BuildContext, tool);

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
        
        foreach (var tool in BuildContext.Tools.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building tool '{0}'", tool);

            var projectFileName = GetProjectFileName(BuildContext, tool);
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, tool);
            
            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(BuildContext, tool);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);

            // SourceLink specific stuff
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
        var version = BuildContext.General.Version.NuGet;

        foreach (var tool in BuildContext.Tools.Items)
        {
            if (!ShouldDeployProject(BuildContext, tool))
            {
                CakeContext.Information("Tool '{0}' should not be deployed", tool);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Packaging tool '{0}'", tool);

            var projectDirectory = string.Format("./src/{0}", tool);
            var projectFileName = string.Format("{0}/{1}.csproj", projectDirectory, tool);
            var outputDirectory = GetProjectOutputDirectory(BuildContext, tool);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);

            // Step 1: remove intermediate files to ensure we have the same results on the build server, somehow NuGet 
            // targets tries to find the resource assemblies in [ProjectName]\obj\Release\net46\de\[ProjectName].resources.dll',
            // we won't run a clean on the project since it will clean out the actual output (which we still need for packaging)

            CakeContext.Information("Cleaning intermediate files for tool '{0}'", tool);

            var binFolderPattern = string.Format("{0}/bin/{1}/**.dll", projectDirectory, configurationName);

            CakeContext.Information("Deleting 'bin' directory contents using '{0}'", binFolderPattern);

            var binFiles = CakeContext.GetFiles(binFolderPattern);
            CakeContext.DeleteFiles(binFiles);

            var objFolderPattern = string.Format("{0}/obj/{1}/**.dll", projectDirectory, configurationName);

            CakeContext.Information("Deleting 'bin' directory contents using '{0}'", objFolderPattern);

            var objFiles = CakeContext.GetFiles(objFolderPattern);
            CakeContext.DeleteFiles(objFiles);

            CakeContext.Information(string.Empty);

            // Step 2: Ensure chocolatey stuff
            EnsureChocolateyLicenseFile(tool);
            EnsureChocolateyVerificationFile(tool);

            // Step 3: Go packaging!
            CakeContext.Information("Using 'msbuild' to package '{0}'", tool);

            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = configurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, tool, "pack");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            msBuildSettings.WithProperty("OverridableOutputRootPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", BuildContext.General.OutputRootDirectory);
            msBuildSettings.WithProperty("ConfigurationName", configurationName);
            msBuildSettings.WithProperty("PackageVersion", version);

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
            
            // As described in the this issue: https://github.com/NuGet/Home/issues/4360
            // we should not use IsTool, but set BuildOutputTargetFolder instead
            msBuildSettings.WithProperty("BuildOutputTargetFolder", "tools");
            msBuildSettings.WithProperty("NoDefaultExcludes", "true");
            //msBuildSettings.WithProperty("IsTool", "true");

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
                CakeContext.Information("Signing NuGet package '{0}' using certificate subject '{1}'", 
                    fileToSign, BuildContext.General.CodeSign.CertificateSubjectName);

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

        var version = BuildContext.General.Version.NuGet;

        foreach (var tool in BuildContext.Tools.Items)
        {
            if (!ShouldDeployProject(BuildContext, tool))
            {
                CakeContext.Information("Tool '{0}' should not be deployed", tool);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying tool '{0}'", tool);

            var packageToPush = string.Format("{0}/{1}.{2}.nupkg", BuildContext.General.OutputRootDirectory, tool, version);
            var nuGetRepositoryUrls = GetToolsNuGetRepositoryUrls(tool);
            var nuGetRepositoryApiKeys = GetToolsNuGetRepositoryApiKeys(tool);

            var nuGetServers = GetNuGetServers(nuGetRepositoryUrls, nuGetRepositoryApiKeys);
            if (nuGetServers.Count == 0)
            {
                throw new Exception("No NuGet repositories specified, as a protection mechanism this must *always* be specified to make sure packages aren't accidentally deployed to the default public NuGet feed");
            }

            CakeContext.Information("Found '{0}' target NuGet servers to push tool '{1}'", nuGetServers.Count, tool);

            foreach (var nuGetServer in nuGetServers)
            {
                CakeContext.Information("Pushing to '{0}'", nuGetServer);

                CakeContext.NuGetPush(packageToPush, new NuGetPushSettings
                {
                    Source = nuGetServer.Url,
                    ApiKey = nuGetServer.ApiKey
                });
            }

            await BuildContext.Notifications.NotifyAsync(tool, string.Format("Deployed to NuGet store(s)"), TargetType.Tool);
        }        
    }

    public override async Task FinalizeAsync()
    {

    }
}