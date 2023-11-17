#addin "nuget:?package=Cake.Squirrel&version=0.15.2"

#tool "nuget:?package=Squirrel.Windows&version=2.0.1"

//-------------------------------------------------------------

public class SquirrelInstaller : IInstaller
{
    public SquirrelInstaller(BuildContext buildContext)
    {
        BuildContext = buildContext;

        IsEnabled = BuildContext.BuildServer.GetVariableAsBool("SquirrelEnabled", true, showValue: true);

        if (IsEnabled)
        {
            // In the future, check if Squirrel is installed. Log error if not
            IsAvailable = IsEnabled;
        }
    }

    public BuildContext BuildContext { get; private set; }

    public bool IsEnabled { get; private set; }

    public bool IsAvailable { get; private set; }

    //-------------------------------------------------------------

    public async Task PackageAsync(string projectName, string channel)
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("Squirrel is not enabled or available, skipping integration");
            return;
        }

        // There are 2 flavors:
        //
        // 1: Non-grouped:              /[app]/[channel] (e.g. /MyApp/alpha)
        // Updates will always be applied, even to new major versions
        //
        // 2: Grouped by major version: /[app]/[major_version]/[channel] (e.g. /MyApp/4/alpha)
        // Updates will only be applied to non-major updates. This allows manual migration to
        // new major versions, which is very useful when there are dependencies that need to
        // be updated before a new major version can be switched to.
        var squirrelOutputRoot = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "squirrel", projectName);

        if (BuildContext.Wpf.GroupUpdatesByMajorVersion)
        {
            squirrelOutputRoot = System.IO.Path.Combine(squirrelOutputRoot, BuildContext.General.Version.Major);
        }

        squirrelOutputRoot = System.IO.Path.Combine(squirrelOutputRoot, channel);

        var squirrelReleasesRoot = System.IO.Path.Combine(squirrelOutputRoot, "releases");
        var squirrelOutputIntermediate = System.IO.Path.Combine(squirrelOutputRoot, "intermediate");

        var nuSpecTemplateFileName = System.IO.Path.Combine(".", "deployment", "squirrel", "template", $"{projectName}.nuspec");
        var nuSpecFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectName}.nuspec");
        var nuGetFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectName}.{BuildContext.General.Version.NuGet}.nupkg");

        if (!BuildContext.CakeContext.FileExists(nuSpecTemplateFileName))
        {
            BuildContext.CakeContext.Information($"Skip packaging of WPF app '{projectName}' using Squirrel since no Squirrel template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator($"Packaging WPF app '{projectName}' using Squirrel");

        BuildContext.CakeContext.CreateDirectory(squirrelReleasesRoot);
        BuildContext.CakeContext.CreateDirectory(squirrelOutputIntermediate);

        // Set up Squirrel nuspec
        BuildContext.CakeContext.CopyFile(nuSpecTemplateFileName, nuSpecFileName);

        var setupSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();
        
        // Squirrel does not seem to support . in the names
        var projectSlug = GetProjectSlug(projectName, "_");

        BuildContext.CakeContext.TransformConfig(nuSpecFileName,
            new TransformationCollection 
            {
                { "package/metadata/id", $"{projectSlug}{setupSuffix}" },
                { "package/metadata/version", BuildContext.General.Version.NuGet },
                { "package/metadata/authors", BuildContext.General.Copyright.Company },
                { "package/metadata/owners", BuildContext.General.Copyright.Company },
                { "package/metadata/copyright", string.Format("Copyright © {0} {1} - {2}", BuildContext.General.Copyright.Company, BuildContext.General.Copyright.StartYear, DateTime.Now.Year) },
            });

        var fileContents = System.IO.File.ReadAllText(nuSpecFileName);
        fileContents = fileContents.Replace("[CHANNEL_SUFFIX]", setupSuffix);
        fileContents = fileContents.Replace("[CHANNEL]", BuildContext.Installer.GetDeploymentChannelSuffix(" (", ")"));
        System.IO.File.WriteAllText(nuSpecFileName, fileContents);

        // Copy all files to the lib so Squirrel knows what to do
        var appSourceDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, projectName);
        var appTargetDirectory = System.IO.Path.Combine(squirrelOutputIntermediate, "lib");

        BuildContext.CakeContext.Information($"Copying files from '{appSourceDirectory}' => '{appTargetDirectory}'");

        BuildContext.CakeContext.CopyDirectory(appSourceDirectory, appTargetDirectory);

        var squirrelSourceFile = BuildContext.CakeContext.GetFiles("./tools/squirrel.windows.*/tools/Squirrel.exe").Single();

        // We need to be 1 level deeper, let's just walk each directory in case we can support multi-platform releases
        // in the future
        foreach (var subDirectory in BuildContext.CakeContext.GetSubDirectories(appTargetDirectory))
        {
            var squirrelTargetFile = System.IO.Path.Combine(appTargetDirectory, subDirectory.Segments[subDirectory.Segments.Length - 1], "Squirrel.exe");

            BuildContext.CakeContext.Information($"Copying Squirrel.exe to support self-updates from '{squirrelSourceFile}' => '{squirrelTargetFile}'");

            BuildContext.CakeContext.CopyFile(squirrelSourceFile, squirrelTargetFile);
        }

        // Make sure all files are signed before we package them for Squirrel (saves potential errors occurring later in squirrel releasify)
        var signToolCommand = string.Empty;

        if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
        {
            // Note: Squirrel uses it's own sign tool, so make sure to follow their specs
            signToolCommand = string.Format("/a /t {0} /n {1}", BuildContext.General.CodeSign.TimeStampUri, 
                BuildContext.General.CodeSign.CertificateSubjectName);
        }

        var nuGetSettings  = new NuGetPackSettings
        {
            NoPackageAnalysis = true,
            OutputDirectory = squirrelOutputIntermediate,
            Verbosity = NuGetVerbosity.Detailed,
        };

        // Fix for target framework issues
        nuGetSettings.Properties.Add("TargetPlatformVersion", "7.0");

        // Create NuGet package
        BuildContext.CakeContext.NuGetPack(nuSpecFileName, nuGetSettings);

        // Rename so we have the right nuget package file names (without the channel)
        if (!string.IsNullOrWhiteSpace(setupSuffix))
        {
            var sourcePackageFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectSlug}{setupSuffix}.{BuildContext.General.Version.NuGet}.nupkg");
            var targetPackageFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectName}.{BuildContext.General.Version.NuGet}.nupkg");

            BuildContext.CakeContext.Information($"Moving file from '{sourcePackageFileName}' => '{targetPackageFileName}'");

            BuildContext.CakeContext.MoveFile(sourcePackageFileName, targetPackageFileName);
        }

        // Copy deployments share to the intermediate root so we can locally create the Squirrel releases
        
        var releasesSourceDirectory = GetDeploymentsShareRootDirectory(projectName, channel);
        var releasesTargetDirectory = squirrelReleasesRoot;

        BuildContext.CakeContext.CreateDirectory(releasesSourceDirectory);
        BuildContext.CakeContext.CreateDirectory(releasesTargetDirectory);

        BuildContext.CakeContext.Information($"Copying releases from '{releasesSourceDirectory}' => '{releasesTargetDirectory}'");

        BuildContext.CakeContext.CopyDirectory(releasesSourceDirectory, releasesTargetDirectory);

        // Squirrelify!
        var squirrelSettings = new SquirrelSettings();
        squirrelSettings.Silent = false;
        squirrelSettings.NoMsi = false;
        squirrelSettings.ReleaseDirectory = squirrelReleasesRoot;
        squirrelSettings.LoadingGif = System.IO.Path.Combine(".", "deployment", "squirrel", "loading.gif");

        // Note: this is not really generic, but this is where we store our icons file, we can
        // always change this in the future
        var iconFileName = System.IO.Path.Combine(".", "design", "logo", $"logo{setupSuffix}.ico");
        squirrelSettings.Icon = iconFileName;
        squirrelSettings.SetupIcon = iconFileName;

        if (!string.IsNullOrWhiteSpace(signToolCommand))
        {
            squirrelSettings.SigningParameters = signToolCommand;
        }

        BuildContext.CakeContext.Information("Generating Squirrel packages, this can take a while, especially when signing is enabled...");

        BuildContext.CakeContext.Squirrel(nuGetFileName, squirrelSettings, true, false);

        if (BuildContext.Wpf.UpdateDeploymentsShare)
        {
            BuildContext.CakeContext.Information($"Copying updated Squirrel files back to deployments share at '{releasesSourceDirectory}'");

            // Copy the following files:
            // - [version]-full.nupkg
            // - [version]-full.nupkg
            // - Setup.exe => Setup.exe & WpfApp.exe
            // - Setup.msi
            // - RELEASES

            var squirrelFiles = BuildContext.CakeContext.GetFiles($"{squirrelReleasesRoot}/{projectSlug}{setupSuffix}-{BuildContext.General.Version.NuGet}*.nupkg");
            BuildContext.CakeContext.CopyFiles(squirrelFiles, releasesSourceDirectory);
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(squirrelReleasesRoot, "Setup.exe"), System.IO.Path.Combine(releasesSourceDirectory, "Setup.exe"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(squirrelReleasesRoot, "Setup.exe"), System.IO.Path.Combine(releasesSourceDirectory, $"{projectName}.exe"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(squirrelReleasesRoot, "Setup.msi"), System.IO.Path.Combine(releasesSourceDirectory, "Setup.msi"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(squirrelReleasesRoot, "RELEASES"), System.IO.Path.Combine(releasesSourceDirectory, "RELEASES"));
        }
    }

    //-------------------------------------------------------------

    public async Task<DeploymentTarget> GenerateDeploymentTargetAsync(string projectName)
    {
        var deploymentTarget = new DeploymentTarget
        {
            Name = "Squirrel"
        };

        var channels = new [] 
        {
            "alpha",
            "beta",
            "stable"
        };

        var deploymentGroupNames = new List<string>();
        var projectDeploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);

        if (BuildContext.Wpf.GroupUpdatesByMajorVersion)
        {
            // Check every directory that we can parse as number
            var directories = System.IO.Directory.GetDirectories(projectDeploymentShare);
            
            foreach (var directory in directories)
            {
                var deploymentGroupName = new System.IO.DirectoryInfo(directory).Name;

                if (int.TryParse(deploymentGroupName, out _))
                {
                    deploymentGroupNames.Add(deploymentGroupName);
                }
            }
        }
        else
        {
            // Just a single group
            deploymentGroupNames.Add("all");
        }

        foreach (var deploymentGroupName in deploymentGroupNames)
        {
            BuildContext.CakeContext.Information($"Searching for releases for deployment group '{deploymentGroupName}'");

            var deploymentGroup = new DeploymentGroup
            {
                Name = deploymentGroupName
            };

            var version = deploymentGroupName;
            if (version == "all")
            {
                version = string.Empty;
            }

            foreach (var channel in channels)
            {
                BuildContext.CakeContext.Information($"Searching for releases for deployment channel '{deploymentGroupName}/{channel}'");

                var deploymentChannel = new DeploymentChannel
                {
                    Name = channel
                };

                var targetDirectory = GetDeploymentsShareRootDirectory(projectName, channel, version);

                BuildContext.CakeContext.Information($"Searching for release files in '{targetDirectory}'");

                var fullNupkgFiles = System.IO.Directory.GetFiles(targetDirectory, "*-full.nupkg");

                foreach (var fullNupkgFile in fullNupkgFiles)
                {
                    BuildContext.CakeContext.Information($"Applying release based on '{fullNupkgFile}'");

                    var fullReleaseFileInfo = new System.IO.FileInfo(fullNupkgFile);
                    var fullRelativeFileName = new DirectoryPath(projectDeploymentShare).GetRelativePath(new FilePath(fullReleaseFileInfo.FullName)).FullPath.Replace("\\", "/");
                    
                    var releaseVersion = fullReleaseFileInfo.Name
                        .Replace($"{projectName}_{channel}-", string.Empty)
                        .Replace($"-full.nupkg", string.Empty);

                    // Exception for full releases, they don't contain the channel name
                    if (channel == "stable")
                    {
                        releaseVersion = releaseVersion.Replace($"{projectName}-", string.Empty);
                    }

                    var release = new DeploymentRelease
                    {
                        Name = releaseVersion,
                        Timestamp = fullReleaseFileInfo.CreationTimeUtc
                    };

                    // Full release
                    release.Full = new DeploymentReleasePart
                    {
                        RelativeFileName = fullRelativeFileName,
                        Size = (ulong)fullReleaseFileInfo.Length
                    };

                    // Delta release
                    var deltaNupkgFile = fullNupkgFile.Replace("-full.nupkg", "-delta.nupkg");
                    if (System.IO.File.Exists(deltaNupkgFile))
                    {
                        var deltaReleaseFileInfo = new System.IO.FileInfo(deltaNupkgFile);
                        var deltafullRelativeFileName = new DirectoryPath(projectDeploymentShare).GetRelativePath(new FilePath(deltaReleaseFileInfo.FullName)).FullPath.Replace("\\", "/");
                        
                        release.Delta = new DeploymentReleasePart
                        {
                            RelativeFileName = deltafullRelativeFileName,
                            Size = (ulong)deltaReleaseFileInfo.Length
                        };
                    }

                    deploymentChannel.Releases.Add(release);
                }

                deploymentGroup.Channels.Add(deploymentChannel);
            }

            deploymentTarget.Groups.Add(deploymentGroup);
        }

        return deploymentTarget;
    }

    //-------------------------------------------------------------

    private string GetDeploymentsShareRootDirectory(string projectName, string channel)
    {
        var version = string.Empty;

        if (BuildContext.Wpf.GroupUpdatesByMajorVersion)
        {
            version = BuildContext.General.Version.Major;
        }

        return GetDeploymentsShareRootDirectory(projectName, channel, version);
    }

    //-------------------------------------------------------------
        
    private string GetDeploymentsShareRootDirectory(string projectName, string channel, string version)
    {
        var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);

        if (!string.IsNullOrWhiteSpace(version))
        {
            deploymentShare = System.IO.Path.Combine(deploymentShare, version);
        }

        var installersOnDeploymentsShare = System.IO.Path.Combine(deploymentShare, channel);
        BuildContext.CakeContext.CreateDirectory(installersOnDeploymentsShare);

        return installersOnDeploymentsShare;
    }
}