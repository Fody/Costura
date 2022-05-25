#l "apps-wpf-variables.cake"

#tool "nuget:?package=AzureStorageSync&version=2.0.0-alpha0039&prerelease"

//-------------------------------------------------------------

public class WpfProcessor : ProcessorBase
{
    public WpfProcessor(BuildContext buildContext)
        : base(buildContext)
    {
    }

    public override bool HasItems()
    {
        return BuildContext.Wpf.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var wpfApp in BuildContext.Wpf.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, wpfApp))
            {
                BuildContext.Wpf.Items.Remove(wpfApp);
            }
        }
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // No specific implementation required for now   
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        foreach (var wpfApp in BuildContext.Wpf.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building WPF app '{0}'", wpfApp);

            var projectFileName = GetProjectFileName(BuildContext, wpfApp);
            
            var channelSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();

            var sourceFileName = System.IO.Path.Combine(".", "design", "logo", $"logo{channelSuffix}.ico");
            if (BuildContext.CakeContext.FileExists(sourceFileName))
            {
                CakeContext.Information("Enforcing channel specific icon '{0}'", sourceFileName);

                var projectDirectory = GetProjectDirectory(wpfApp);
                var targetFileName = System.IO.Path.Combine(projectDirectory, "Resources", "Icons", "logo.ico");

                BuildContext.CakeContext.CopyFile(sourceFileName, targetFileName);
            }

            var msBuildSettings = new MSBuildSettings 
            {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, wpfApp, "build");

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            RunMsBuild(BuildContext, wpfApp, projectFileName, msBuildSettings, "build");
        }
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        if (string.IsNullOrWhiteSpace(BuildContext.Wpf.DeploymentsShare))
        {
            CakeContext.Warning("DeploymentsShare variable is not set, cannot package WPF apps");
            return;
        }

        var channels = new List<string>();

        if (BuildContext.General.IsOfficialBuild)
        {
            // Note: we used to deploy stable to stable, beta and alpha, but want to keep things separated now
            channels.Add("stable");
        }
        else if (BuildContext.General.IsBetaBuild)
        {
            // Note: we used to deploy beta to beta and alpha, but want to keep things separated now
            channels.Add("beta");
        }
        else if (BuildContext.General.IsAlphaBuild)
        {
            // Single channel
            channels.Add("alpha");
        }
        else
        {
            // Unknown build type, just just a single channel
            channels.Add(BuildContext.Wpf.Channel);
        }

        CakeContext.Information($"Found '{channels.Count}' target channels");

        foreach (var wpfApp in BuildContext.Wpf.Items)
        {
            if (!ShouldDeployProject(BuildContext, wpfApp))
            {
                CakeContext.Information($"WPF app '{wpfApp}' should not be deployed");
                continue;
            }

            var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(wpfApp);

            CakeContext.Information($"Using deployment share '{deploymentShare}' for WPF app '{wpfApp}'");

            System.IO.Directory.CreateDirectory(deploymentShare);

            CakeContext.Information($"Deleting unnecessary files for WPF app '{wpfApp}'");
            
            var outputDirectory = GetProjectOutputDirectory(BuildContext, wpfApp);
            var extensionsToDelete = new [] { ".pdb", ".RoslynCA.json" };
            
            foreach (var extensionToDelete in extensionsToDelete)
            {
                var searchPattern = $"{outputDirectory}/**/*{extensionToDelete}";
                var filesToDelete = CakeContext.GetFiles(searchPattern);

                CakeContext.Information("Deleting '{0}' files using search pattern '{1}'", filesToDelete.Count, searchPattern);
                
                CakeContext.DeleteFiles(filesToDelete);
            }

            // We know we *highly likely* need to sign, so try doing this upfront
            if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
            {
                BuildContext.CakeContext.Information("Searching for packagable files to sign:");

                var projectFilesToSign = new List<FilePath>();

                var exeSignFilesSearchPattern = $"{BuildContext.General.OutputRootDirectory}/{wpfApp}/**/*.exe";
                BuildContext.CakeContext.Information($"  - {exeSignFilesSearchPattern}");
                projectFilesToSign.AddRange(BuildContext.CakeContext.GetFiles(exeSignFilesSearchPattern));

                var dllSignFilesSearchPattern = $"{BuildContext.General.OutputRootDirectory}/{wpfApp}/**/*.dll";
                BuildContext.CakeContext.Information($"  - {dllSignFilesSearchPattern}");
                projectFilesToSign.AddRange(BuildContext.CakeContext.GetFiles(dllSignFilesSearchPattern));

                var signToolCommand = string.Format("sign /a /t {0} /n {1}", BuildContext.General.CodeSign.TimeStampUri, 
                    BuildContext.General.CodeSign.CertificateSubjectName);

                SignFiles(BuildContext, signToolCommand, projectFilesToSign);
            }            
            else
            {
                BuildContext.CakeContext.Warning("No signing certificate subject name provided, not signing any files");
            }

            foreach (var channel in channels)
            {
                CakeContext.Information("Packaging app '{0}' for channel '{1}'", wpfApp, channel);

                var deploymentShareForChannel = System.IO.Path.Combine(deploymentShare, channel);
                System.IO.Directory.CreateDirectory(deploymentShareForChannel);

                await BuildContext.Installer.PackageAsync(wpfApp, channel);
            }
        }   
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        var azureConnectionString = BuildContext.Wpf.AzureDeploymentsStorageConnectionString;
        if (string.IsNullOrWhiteSpace(azureConnectionString))
        {
            CakeContext.Warning("Skipping deployments of WPF apps because not Azure deployments storage connection string was specified");
            return;
        }
        
        var azureStorageSyncExes = CakeContext.GetFiles("./tools/AzureStorageSync*/**/AzureStorageSync.exe");
        var azureStorageSyncExe = azureStorageSyncExes.LastOrDefault();
        if (azureStorageSyncExe is null)
        {
            throw new Exception("Can't find the AzureStorageSync tool that should have been installed via this script");
        }

        foreach (var wpfApp in BuildContext.Wpf.Items)
        {
            if (!ShouldDeployProject(BuildContext, wpfApp))
            {
                CakeContext.Information($"WPF app '{wpfApp}' should not be deployed");
                continue;
            }
            
            BuildContext.CakeContext.LogSeparator($"Deploying WPF app '{wpfApp}'");

            // TODO: Respect the deploy settings per category, requires changes to AzureStorageSync
            if (!BuildContext.Wpf.DeployUpdatesToAlphaChannel ||
                !BuildContext.Wpf.DeployUpdatesToBetaChannel ||
                !BuildContext.Wpf.DeployUpdatesToStableChannel ||
                !BuildContext.Wpf.DeployInstallers)
            {
                throw new Exception("Not deploying a specific channel is not yet supported, please implement");
            }

            //%DeploymentsShare%\%ProjectName% /%ProjectName% -c %AzureDeploymentsStorageConnectionString%
            var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(wpfApp);
            var projectSlug = GetProjectSlug(wpfApp, "-");

            var exitCode = CakeContext.StartProcess(azureStorageSyncExe, new ProcessSettings
            {
                Arguments = $"{deploymentShare} /{projectSlug} -c {azureConnectionString}"
            });

            if (exitCode != 0)
            {
                throw new Exception($"Received unexpected exit code '{exitCode}' for WPF app '{wpfApp}'");
            }

            await BuildContext.Notifications.NotifyAsync(wpfApp, string.Format("Deployed to target"), TargetType.WpfApp);
        }
    }

    public override async Task FinalizeAsync()
    {

    }
}
