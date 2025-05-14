#tool "dotnet:?package=vpk&version=0.0.1251"

//-------------------------------------------------------------

public class VelopackInstaller : IInstaller
{
    public VelopackInstaller(BuildContext buildContext)
    {
        BuildContext = buildContext;

        IsEnabled = BuildContext.BuildServer.GetVariableAsBool("VelopackEnabled", false, showValue: true);

        if (IsEnabled)
        {
            IsAvailable = IsEnabled;

            // Protection
            if (BuildContext.BuildServer.GetVariableAsBool("SquirrelEnabled", true, showValue: true))
            {
                throw new Exception("Both Velopack and Squirrel are enabled, make sure to disable Squirrel when migrating to Velopack");
            }
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
            BuildContext.CakeContext.Information("Velopack is not enabled or available, skipping integration");
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
        var velopackOutputRoot = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "velopack", projectName);

        if (BuildContext.Wpf.GroupUpdatesByMajorVersion)
        {
            velopackOutputRoot = System.IO.Path.Combine(velopackOutputRoot, BuildContext.General.Version.Major);
        }

        velopackOutputRoot = System.IO.Path.Combine(velopackOutputRoot, channel);

        var velopackReleasesRoot = System.IO.Path.Combine(velopackOutputRoot, "releases");

        BuildContext.CakeContext.LogSeparator($"Packaging WPF app '{projectName}' using Velopack");

        BuildContext.CakeContext.CreateDirectory(velopackReleasesRoot);

        var setupSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();
        
        // Velopack does not seem to support . in the names (keeping same behavior as Squirrel)
        var projectSlug = GetProjectSlug(projectName, "_");

        // Copy all files to the lib so Velopack knows what to do
        var appSourceDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, projectName);

        // Note: there should be only a single target framework, but pick the highest
        var subDirectories = System.IO.Directory.GetDirectories(appSourceDirectory);
        appSourceDirectory = subDirectories.Last();

        // Copy deployments share to the intermediate root so we can locally create the releases
        
        var releasesSourceDirectory = GetDeploymentsShareRootDirectory(projectName, channel);
        var releasesTargetDirectory = velopackReleasesRoot;

        BuildContext.CakeContext.CreateDirectory(releasesSourceDirectory);
        BuildContext.CakeContext.CreateDirectory(releasesTargetDirectory);

        BuildContext.CakeContext.Information($"Copying releases from '{releasesSourceDirectory}' => '{releasesTargetDirectory}'");

        BuildContext.CakeContext.CopyDirectory(releasesSourceDirectory, releasesTargetDirectory);

        BuildContext.CakeContext.Information("Generating Velopack packages, this can take a while, especially when signing is enabled...");

        // Pack using velopack (example command line: vpk pack -u YourAppId -v 1.0.0 -p publish -e yourMainBinary.exe)

        var appId = $"{projectSlug}{setupSuffix}";

        var argumentBuilder = new ProcessArgumentBuilder()
            .Append("pack")
            .Append("--verbose")
            .AppendSwitch("--packId", appId)
            .AppendSwitch("--packVersion", BuildContext.General.Version.NuGet)
            .AppendSwitch("--packDir", appSourceDirectory)
            .AppendSwitch("--packAuthors", BuildContext.General.Copyright.Company)
            .AppendSwitch("--delta", "BestSpeed")
            .AppendSwitch("--outputDir", velopackReleasesRoot);

        // Note: for now BIG assumption that the exe is the same as project name
        argumentBuilder = argumentBuilder
            .AppendSwitch("--mainExe", $"{projectName}.exe");

        // Check several different allowed formats
        var allowedSplashImages = new []
        {
            // Support "channel specific images"
            $"splash_{setupSuffix}.gif",
            $"splash_{setupSuffix}.png",
            "splash.gif",
            "splash.png",
        };

        foreach (var allowedSplashImage in allowedSplashImages)
        {
            var splashImageFileName = System.IO.Path.Combine(".", "deployment", "velopack", allowedSplashImage);
            if (System.IO.File.Exists(splashImageFileName))
            {
                argumentBuilder = argumentBuilder
                    .AppendSwitch("--splashImage", splashImageFileName);
                break;
            }
        }

        // Note: this is not really generic, but this is where we store our icons file, we can
        // always change this in the future
        var iconFileName = System.IO.Path.Combine(".", "design", "logo", $"logo{setupSuffix}.ico");
        argumentBuilder = argumentBuilder
            .AppendSwitch("--icon", iconFileName);

        // --signTemplate {{file}} will be substituted
        // Note that we need to replace / by \ on Windows
        var signToolExe = GetSignToolFileName(BuildContext).Replace("/", "\\");
        var signToolCommandLine = GetSignToolCommandLine(BuildContext);
        if (!string.IsNullOrWhiteSpace(signToolExe) &&
            !string.IsNullOrWhiteSpace(signToolCommandLine))
        {
            // In order to work around a double quote issue (C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe),
            // if 'signtool.exe' is used, use signParams instead
            if (signToolExe.EndsWith("\\signtool.exe"))
            {
                if (signToolCommandLine.StartsWith("sign "))
                {
                    signToolCommandLine = signToolCommandLine.Substring("sign ".Length);
                }

                argumentBuilder = argumentBuilder
                    .AppendSwitch("--signParams", $"\"{signToolCommandLine}\"");
            }
            else
            {
                argumentBuilder = argumentBuilder
                    .AppendSwitch("--signTemplate", $"\"{signToolExe} {signToolCommandLine} {{{{file}}}}\"");
            }       
       }

    	var vpkToolExe = BuildContext.CakeContext.Tools.Resolve("vpk.exe");

        var vpkToolExitCode = BuildContext.CakeContext.StartProcess(vpkToolExe,
            new ProcessSettings
            {    
                Arguments = argumentBuilder
            }
        );

        if (vpkToolExitCode != 0)
        {
            throw new Exception("Failed to pack application");
        }

        // Copy setup
        BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, $"{appId}-win-Setup.exe"), System.IO.Path.Combine(velopackReleasesRoot, "Setup.exe"));

        if (BuildContext.Wpf.UpdateDeploymentsShare)
        {
            BuildContext.CakeContext.Information($"Copying updated Velopack files back to deployments share at '{releasesSourceDirectory}'");

            // Copy the following files:
            // - [version]-delta.nupkg
            // - [version]-full.nupkg
            // - Setup.exe => Setup.exe & WpfApp.exe
            // - releases.win.json
            // - RELEASES

            // Note to consider in future: this stores (and uploads) the same file 4 times. Maybe we need to stop processing so many files
            // to save time on uploads (and eventually money on storage)
            var velopackFiles = BuildContext.CakeContext.GetFiles($"{velopackReleasesRoot}/{appId}-{BuildContext.General.Version.NuGet}*.nupkg");
            BuildContext.CakeContext.CopyFiles(velopackFiles, releasesSourceDirectory);
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, $"{appId}-win-Portable.zip"), System.IO.Path.Combine(releasesSourceDirectory, $"{appId}-win-Portable.zip"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, $"{appId}-win-Setup.exe"), System.IO.Path.Combine(releasesSourceDirectory, $"{appId}-win-Setup.exe"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "Setup.exe"), System.IO.Path.Combine(releasesSourceDirectory, "Setup.exe"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "Setup.exe"), System.IO.Path.Combine(releasesSourceDirectory, $"{projectName}.exe"));
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "releases.win.json"), System.IO.Path.Combine(releasesSourceDirectory, "releases.win.json"));
            
            // Note: RELEASES is there for backwards compatibility
            BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "RELEASES"), System.IO.Path.Combine(releasesSourceDirectory, "RELEASES"));
        }
    }

    //-------------------------------------------------------------

    public async Task<DeploymentTarget> GenerateDeploymentTargetAsync(string projectName)
    {
        var deploymentTarget = new DeploymentTarget
        {
            Name = "Velopack"
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