#pragma warning disable 1998

#l "apps-wpf-variables.cake"

#addin "nuget:?package=Cake.Squirrel&version=0.13.0"
#addin "nuget:?package=MagicChunks&version=2.0.0.119"
//#addin "nuget:?Cake.AzureStorage&version=0.14.0"

#tool "nuget:?package=Squirrel.Windows&version=1.9.1"
#tool "nuget:?package=AzureStorageSync&version=2.0.0-alpha0028&prerelease"

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

    private void PackageWpfAppUsingInnoSetup(string wpfApp, string channel)
    {
        var innoSetupTemplateDirectory = string.Format("./deployment/innosetup/{0}", wpfApp);
        if (!CakeContext.DirectoryExists(innoSetupTemplateDirectory))
        {
            CakeContext.Information("Skip packaging of WPF app '{0}' using Inno Setup since no Inno Setup template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator("Packaging WPF app '{0}' using Inno Setup", wpfApp);

        var installersOnDeploymentsShare = string.Format("{0}/{1}/installer", BuildContext.Wpf.DeploymentsShare, wpfApp);
        CakeContext.CreateDirectory(installersOnDeploymentsShare);

        var setupPostfix = string.Empty;
        if (!string.Equals(channel, "stable", StringComparison.OrdinalIgnoreCase))
        {
            setupPostfix = string.Format("_{0}", channel.ToLower());
        }

        var innoSetupOutputRoot = string.Format("{0}/innosetup/{1}", BuildContext.General.OutputRootDirectory, wpfApp);
        var innoSetupReleasesRoot = string.Format("{0}/releases", innoSetupOutputRoot);
        var innoSetupOutputIntermediate = string.Format("{0}/intermediate", innoSetupOutputRoot);

        CakeContext.CreateDirectory(innoSetupReleasesRoot);
        CakeContext.CreateDirectory(innoSetupOutputIntermediate);

        // Set up InnoSetup template
        CakeContext.CopyDirectory(innoSetupTemplateDirectory, innoSetupOutputIntermediate);

        var innoSetupScriptFileName = string.Format("{0}/setup.iss", innoSetupOutputIntermediate);
        var fileContents = System.IO.File.ReadAllText(innoSetupScriptFileName);
        fileContents = fileContents.Replace("[VERSION]", BuildContext.General.Version.MajorMinorPatch);
        fileContents = fileContents.Replace("[VERSION_DISPLAY]", BuildContext.General.Version.FullSemVer);
        fileContents = fileContents.Replace("[WIZARDIMAGEFILE]", string.Format("logo_large{0}", setupPostfix));

        var signTool = string.Empty;
        if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
        {
            signTool = string.Format("SignTool={0}", BuildContext.General.CodeSign.CertificateSubjectName);
        }

        fileContents = fileContents.Replace("[SIGNTOOL]", signTool);
        System.IO.File.WriteAllText(innoSetupScriptFileName, fileContents);

        // Copy all files to the intermediate directory so Inno Setup knows what to do
        var appSourceDirectory = string.Format("{0}/{1}/**/*", BuildContext.General.OutputRootDirectory, wpfApp);
        var appTargetDirectory = innoSetupOutputIntermediate;

        CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        CakeContext.CopyFiles(appSourceDirectory, appTargetDirectory, true);

        CakeContext.Information("Generating Inno Setup packages, this can take a while, especially when signing is enabled...");

        CakeContext.InnoSetup(innoSetupScriptFileName, new InnoSetupSettings
        {
            OutputDirectory = innoSetupReleasesRoot
        });

        if (BuildContext.Wpf.UpdateDeploymentsShare)
        {
            CakeContext.Information("Copying Inno Setup files to deployments share at '{0}'", installersOnDeploymentsShare);

            // Copy the following files:
            // - Setup.exe => [wpfApp]-[version].exe
            // - Setup.exe => [wpfApp]-[channel].exe

            var installerSourceFile = string.Format("{0}/{1}_{2}.exe", innoSetupReleasesRoot, wpfApp, BuildContext.General.Version.FullSemVer);
            CakeContext.CopyFile(installerSourceFile, string.Format("{0}/{1}_{2}.exe", installersOnDeploymentsShare, wpfApp, BuildContext.General.Version.FullSemVer));
            CakeContext.CopyFile(installerSourceFile, string.Format("{0}/{1}{2}.exe", installersOnDeploymentsShare, wpfApp, setupPostfix));
        }
    }

    private void PackageWpfAppUsingSquirrel(string wpfApp, string channel)
    {
        var squirrelOutputRoot = string.Format("{0}/squirrel/{1}/{2}", BuildContext.General.OutputRootDirectory, wpfApp, channel);
        var squirrelReleasesRoot = string.Format("{0}/releases", squirrelOutputRoot);
        var squirrelOutputIntermediate = string.Format("{0}/intermediate", squirrelOutputRoot);

        var nuSpecTemplateFileName = string.Format("./deployment/squirrel/template/{0}.nuspec", wpfApp);
        var nuSpecFileName = string.Format("{0}/{1}.nuspec", squirrelOutputIntermediate, wpfApp);
        var nuGetFileName = string.Format("{0}/{1}.{2}.nupkg", squirrelOutputIntermediate, wpfApp, BuildContext.General.Version.NuGet);

        if (!CakeContext.FileExists(nuSpecTemplateFileName))
        {
            CakeContext.Information("Skip packaging of WPF app '{0}' using Squirrel since no Squirrel template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator("Packaging WPF app '{0}' using Squirrel", wpfApp);

        CakeContext.CreateDirectory(squirrelReleasesRoot);
        CakeContext.CreateDirectory(squirrelOutputIntermediate);

        // Set up Squirrel nuspec
        CakeContext.CopyFile(nuSpecTemplateFileName, nuSpecFileName);

        CakeContext.TransformConfig(nuSpecFileName,
            new TransformationCollection {
                { "package/metadata/version", BuildContext.General.Version.NuGet },
                { "package/metadata/authors", BuildContext.General.Copyright.Company },
                { "package/metadata/owners", BuildContext.General.Copyright.Company },
                { "package/metadata/copyright", string.Format("Copyright © {0} {1} - {2}", BuildContext.General.Copyright.Company, BuildContext.General.Copyright.StartYear, DateTime.Now.Year) },
            });

        // Copy all files to the lib so Squirrel knows what to do
        var appSourceDirectory = string.Format("{0}/{1}", BuildContext.General.OutputRootDirectory, wpfApp);
        var appTargetDirectory = string.Format("{0}/lib", squirrelOutputIntermediate);

        CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        CakeContext.CopyDirectory(appSourceDirectory, appTargetDirectory);

        // Create NuGet package
        CakeContext.NuGetPack(nuSpecFileName, new NuGetPackSettings
        {
            OutputDirectory = squirrelOutputIntermediate,
        });

        // Copy deployments share to the intermediate root so we can locally create the Squirrel releases
        var releasesSourceDirectory = string.Format("{0}/{1}/{2}", BuildContext.Wpf.DeploymentsShare, wpfApp, channel);
        var releasesTargetDirectory = squirrelReleasesRoot;

        CakeContext.Information("Copying releases from '{0}' => '{1}'", releasesSourceDirectory, releasesTargetDirectory);

        CakeContext.CopyDirectory(releasesSourceDirectory, releasesTargetDirectory);

        // Squirrelify!
        var squirrelSettings = new SquirrelSettings();
        squirrelSettings.NoMsi = false;
        squirrelSettings.ReleaseDirectory = squirrelReleasesRoot;
        squirrelSettings.LoadingGif = "./deployment/squirrel/loading.gif";

        // Note: this is not really generic, but this is where we store our icons file, we can
        // always change this in the future
        var iconFileName = "./design/logo/logo.ico";
        squirrelSettings.Icon = iconFileName;
        squirrelSettings.SetupIcon = iconFileName;

        if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
        {
            squirrelSettings.SigningParameters = string.Format("/a /t {0} /n {1}", BuildContext.General.CodeSign.TimeStampUri, 
                BuildContext.General.CodeSign.CertificateSubjectName);
        }

        CakeContext.Information("Generating Squirrel packages, this can take a while, especially when signing is enabled...");

        CakeContext.Squirrel(nuGetFileName, squirrelSettings);

        if (BuildContext.Wpf.UpdateDeploymentsShare)
        {
            CakeContext.Information("Copying updated Squirrel files back to deployments share at '{0}'", releasesSourceDirectory);

            // Copy the following files:
            // - [version]-full.nupkg
            // - [version]-full.nupkg
            // - Setup.exe => Setup.exe & WpfApp.exe
            // - Setup.msi
            // - RELEASES

            var squirrelFiles = CakeContext.GetFiles(string.Format("{0}/{1}-{2}*.nupkg", squirrelReleasesRoot, wpfApp, BuildContext.General.Version.NuGet));
            CakeContext.CopyFiles(squirrelFiles, releasesSourceDirectory);
            CakeContext.CopyFile(string.Format("{0}/Setup.exe", squirrelReleasesRoot), string.Format("{0}/Setup.exe", releasesSourceDirectory));
            CakeContext.CopyFile(string.Format("{0}/Setup.exe", squirrelReleasesRoot), string.Format("{0}/{1}.exe", releasesSourceDirectory, wpfApp));
            CakeContext.CopyFile(string.Format("{0}/Setup.msi", squirrelReleasesRoot), string.Format("{0}/Setup.msi", releasesSourceDirectory));
            CakeContext.CopyFile(string.Format("{0}/RELEASES", squirrelReleasesRoot), string.Format("{0}/RELEASES", releasesSourceDirectory));
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
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, wpfApp);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(BuildContext, wpfApp);
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
        
        if (string.IsNullOrWhiteSpace(BuildContext.Wpf.DeploymentsShare))
        {
            CakeContext.Warning("DeploymentsShare variable is not set, cannot package WPF apps");
            return;
        }

        var channels = new List<string>();

        if (BuildContext.General.IsOfficialBuild)
        {
            // All channels
            channels.Add("alpha");
            channels.Add("beta");
            channels.Add("stable");
        }
        else if (BuildContext.General.IsBetaBuild)
        {
            // Both alpha and beta, since MyApp.beta1 should also be available on the alpha channel
            channels.Add("alpha");
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

        CakeContext.Information("Found '{0}' target channels", channels.Count);

        foreach (var wpfApp in BuildContext.Wpf.Items)
        {
            CakeContext.Information("Deleting unnecessary files for WPF app '{0}'", wpfApp);
            
            var outputDirectory = GetProjectOutputDirectory(BuildContext, wpfApp);
            var extensionsToDelete = new [] { ".pdb", ".RoslynCA.json" };
            
            foreach (var extensionToDelete in extensionsToDelete)
            {
                var searchPattern = string.Format("{0}**/*{1}", outputDirectory, extensionToDelete);
                var filesToDelete = CakeContext.GetFiles(searchPattern);

                CakeContext.Information("Deleting '{0}' files using search pattern '{1}'", filesToDelete.Count, searchPattern);
                
                CakeContext.DeleteFiles(filesToDelete);
            }

            foreach (var channel in channels)
            {
                CakeContext.Information("Packaging WPF app '{0}' for channel '{1}'", wpfApp, channel);

                PackageWpfAppUsingInnoSetup(wpfApp, channel);
                PackageWpfAppUsingSquirrel(wpfApp, channel);
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
                CakeContext.Information("WPF app '{0}' should not be deployed", wpfApp);
                continue;
            }
            
            BuildContext.CakeContext.LogSeparator("Deploying WPF app '{0}'", wpfApp);

            //%DeploymentsShare%\%ProjectName% /%ProjectName% -c %AzureDeploymentsStorageConnectionString%
            var deploymentShare = string.Format("{0}/{1}", BuildContext.Wpf.DeploymentsShare, wpfApp);

            var exitCode = CakeContext.StartProcess(azureStorageSyncExe, new ProcessSettings
            {
                Arguments = string.Format("{0} /{1} -c {2}", deploymentShare, wpfApp, azureConnectionString)
            });

            if (exitCode != 0)
            {
                throw new Exception(string.Format("Received unexpected exit code '{0}' for WPF app '{1}'", exitCode, wpfApp));
            }

            await BuildContext.Notifications.NotifyAsync(wpfApp, string.Format("Deployed to target"), TargetType.WpfApp);
        }
    }

    public override async Task FinalizeAsync()
    {

    }
}
