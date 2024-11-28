//-------------------------------------------------------------

public class MsixInstaller : IInstaller
{
    public MsixInstaller(BuildContext buildContext)
    {
        BuildContext = buildContext;

        Publisher = BuildContext.BuildServer.GetVariable("MsixPublisher", showValue: true);
        UpdateUrl = BuildContext.BuildServer.GetVariable("MsixUpdateUrl", showValue: true);
        IsEnabled = BuildContext.BuildServer.GetVariableAsBool("MsixEnabled", true, showValue: true);

        if (IsEnabled)
        {
            // In the future, check if Msix is installed. Log error if not
            IsAvailable = IsEnabled;
        }
    }

    public BuildContext BuildContext { get; private set; }

    public string Publisher { get; private set; }

    public string UpdateUrl { get; private set; }

    public bool IsEnabled { get; private set; }

    public bool IsAvailable { get; private set; }

    //-------------------------------------------------------------

    public async Task PackageAsync(string projectName, string channel)
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("MSIX is not enabled or available, skipping integration");
            return;
        }

        var makeAppxFileName = FindLatestMakeAppxFileName();
        if (!BuildContext.CakeContext.FileExists(makeAppxFileName))
        {
            BuildContext.CakeContext.Information("Could not find MakeAppX.exe, skipping MSIX integration");
            return;
        }

        var msixTemplateDirectory = System.IO.Path.Combine(".", "deployment", "msix", projectName);
        if (!BuildContext.CakeContext.DirectoryExists(msixTemplateDirectory))
        {
            BuildContext.CakeContext.Information($"Skip packaging of app '{projectName}' using MSIX since no MSIX template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator($"Packaging app '{projectName}' using MSIX");

        var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);
        var installersOnDeploymentsShare = GetDeploymentsShareRootDirectory(projectName, channel);

        var setupSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();

        var msixOutputRoot = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "msix", projectName);
        var msixReleasesRoot = System.IO.Path.Combine(msixOutputRoot, "releases");
        var msixOutputIntermediate = System.IO.Path.Combine(msixOutputRoot, "intermediate");

        BuildContext.CakeContext.CreateDirectory(msixReleasesRoot);
        BuildContext.CakeContext.CreateDirectory(msixOutputIntermediate);

        // Set up MSIX template, all based on the documentation here: https://docs.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion
        BuildContext.CakeContext.CopyDirectory(msixTemplateDirectory, msixOutputIntermediate);

        var msixInstallerName = $"{projectName}_{BuildContext.General.Version.FullSemVer}.msix";
        var installerSourceFile = System.IO.Path.Combine(msixReleasesRoot, msixInstallerName);

        var variables = new Dictionary<string, string>();
        variables["[PRODUCT]"] = projectName;
        variables["[PRODUCT_WITH_CHANNEL]"] = projectName + BuildContext.Installer.GetDeploymentChannelSuffix("");
        variables["[PRODUCT_WITH_CHANNEL_DISPLAY]"] = projectName + BuildContext.Installer.GetDeploymentChannelSuffix(" (", ")");
        variables["[PUBLISHER]"] = Publisher;
        variables["[PUBLISHER_DISPLAY]"] = BuildContext.General.Copyright.Company;
        variables["[CHANNEL_SUFFIX]"] = setupSuffix;
        variables["[CHANNEL]"] = BuildContext.Installer.GetDeploymentChannelSuffix(" (", ")");
        variables["[VERSION]"] = BuildContext.General.Version.MajorMinorPatch;
        variables["[VERSION_WITH_REVISION]"] = $"{BuildContext.General.Version.MajorMinorPatch}.{BuildContext.General.Version.CommitsSinceVersionSource}";
        variables["[VERSION_DISPLAY]"] = BuildContext.General.Version.FullSemVer;
        variables["[WIZARDIMAGEFILE]"] = string.Format("logo_large{0}", setupSuffix);

        // Important: urls must be lower case, they are case sensitive in azure blob storage
        variables["[URL_APPINSTALLER]"] = $"{UpdateUrl}/{projectName}/{channel}/msix/{projectName}.appinstaller".ToLower();
        variables["[URL_MSIX]"] = $"{UpdateUrl}/{projectName}/{channel}/msix/{msixInstallerName}".ToLower();

        // Installer file
        var msixScriptFileName = System.IO.Path.Combine(msixOutputIntermediate, "AppxManifest.xml");
        
        ReplaceVariablesInFile(msixScriptFileName, variables);

        // Update file
        var msixUpdateScriptFileName = System.IO.Path.Combine(msixOutputIntermediate, "App.AppInstaller");
        if (BuildContext.CakeContext.FileExists(msixUpdateScriptFileName))
        {
            ReplaceVariablesInFile(msixUpdateScriptFileName, variables);
        }

        // Copy all files to the intermediate directory so MSIX knows what to do
        var appSourceDirectory = string.Format("{0}/{1}/**/*", BuildContext.General.OutputRootDirectory, projectName);
        var appTargetDirectory = msixOutputIntermediate;

        BuildContext.CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        BuildContext.CakeContext.CopyFiles(appSourceDirectory, appTargetDirectory, true);

        if (BuildContext.General.CodeSign.IsAvailable ||
            BuildContext.General.AzureCodeSign.IsAvailable)
        {
            SignFilesInDirectory(BuildContext, appTargetDirectory, string.Empty);
        }
        else
        {
            BuildContext.CakeContext.Warning("No sign tool is defined, MSIX will not be installable to (most or all) users");
        }
        
        BuildContext.CakeContext.Information("Generating MSIX packages using MakeAppX...");

        var processSettings = new ProcessSettings
        {
            WorkingDirectory = appTargetDirectory,
        };

        processSettings.WithArguments(a => a.Append("pack")
                                            .AppendSwitchQuoted("/p", installerSourceFile)
                                            //.AppendSwitchQuoted("/m", msixScriptFileName) // If we specify this one, we *must* provide a mappings file, which we don't want to do
                                            //.AppendSwitchQuoted("/f", msixScriptFileName)
                                            .AppendSwitchQuoted("/d", appTargetDirectory)
                                            //.Append("/v")
                                            .Append("/o"));

        using (var process = BuildContext.CakeContext.StartAndReturnProcess(makeAppxFileName, processSettings))
        {
            process.WaitForExit();
            var exitCode = process.GetExitCode();

            if (exitCode != 0)
            {
                throw new Exception($"Packaging failed, exit code is '{exitCode}'");
            }
        }

        SignFile(BuildContext, installerSourceFile);

        // Always copy the AppInstaller if available
        if (BuildContext.CakeContext.FileExists(msixUpdateScriptFileName))
        {
            BuildContext.CakeContext.Information("Copying update manifest to output directory");

            // - App.AppInstaller => [projectName].AppInstaller
            BuildContext.CakeContext.CopyFile(msixUpdateScriptFileName, System.IO.Path.Combine(msixReleasesRoot, $"{projectName}.AppInstaller"));
        }

        if (BuildContext.Wpf.UpdateDeploymentsShare)
        {
            BuildContext.CakeContext.Information("Copying MSIX files to deployments share at '{0}'", installersOnDeploymentsShare);

            // Copy the following files:
            // - [ProjectName]_[version].msix => [projectName]_[version].msix
            // - [ProjectName]_[version].msix => [projectName]_[channel].msix

            BuildContext.CakeContext.CopyFile(installerSourceFile, System.IO.Path.Combine(installersOnDeploymentsShare, msixInstallerName));
            BuildContext.CakeContext.CopyFile(installerSourceFile, System.IO.Path.Combine(installersOnDeploymentsShare, $"{projectName}{setupSuffix}.msix"));

            if (BuildContext.CakeContext.FileExists(msixUpdateScriptFileName))
            {
                // - App.AppInstaller => [projectName].AppInstaller
                BuildContext.CakeContext.CopyFile(msixUpdateScriptFileName, System.IO.Path.Combine(installersOnDeploymentsShare, $"{projectName}.AppInstaller"));
            }
        }
    }

    //-------------------------------------------------------------

    public async Task<DeploymentTarget> GenerateDeploymentTargetAsync(string projectName)
    {
        var deploymentTarget = new DeploymentTarget
        {
            Name = "MSIX"
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

                var msixFiles = System.IO.Directory.GetFiles(targetDirectory, "*.msix");

                foreach (var msixFile in msixFiles)
                {
                    var releaseFileInfo = new System.IO.FileInfo(msixFile);
                    var relativeFileName = new DirectoryPath(projectDeploymentShare).GetRelativePath(new FilePath(releaseFileInfo.FullName)).FullPath.Replace("\\", "/");
                    var releaseVersion = releaseFileInfo.Name
                        .Replace($"{projectName}_", string.Empty)
                        .Replace($".msix", string.Empty);

                    // Either empty or matching a release channel should be ignored
                    if (string.IsNullOrWhiteSpace(releaseVersion) ||
                        channels.Any(x => x == releaseVersion))
                    {
                        BuildContext.CakeContext.Information($"Ignoring '{msixFile}'");
                        continue;
                    }

                    // Special case for stable releases
                    if (channel == "stable")
                    {
                        if (releaseVersion.Contains("-alpha") ||
                            releaseVersion.Contains("-beta"))
                        {
                            BuildContext.CakeContext.Information($"Ignoring '{msixFile}'");
                            continue;
                        }
                    }

                    BuildContext.CakeContext.Information($"Applying release based on '{msixFile}'");

                    var release = new DeploymentRelease
                    {
                        Name = releaseVersion,
                        Timestamp = releaseFileInfo.CreationTimeUtc
                    };

                    // Only support full versions
                    release.Full = new DeploymentReleasePart
                    {
                        RelativeFileName = relativeFileName,
                        Size = (ulong)releaseFileInfo.Length
                    };

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

        var installersOnDeploymentsShare = System.IO.Path.Combine(deploymentShare, channel, "msix");
        BuildContext.CakeContext.CreateDirectory(installersOnDeploymentsShare);

        return installersOnDeploymentsShare;
    }

    //-------------------------------------------------------------

    private void ReplaceVariablesInFile(string fileName, Dictionary<string, string> variables)
    {
        var fileContents = System.IO.File.ReadAllText(fileName);

        foreach (var keyValuePair in variables)
        {
            fileContents = fileContents.Replace(keyValuePair.Key, keyValuePair.Value);
        }

        System.IO.File.WriteAllText(fileName, fileContents);
    }

    //-------------------------------------------------------------

    private string FindLatestMakeAppxFileName()
    {
        var directory = FindLatestWindowsKitsDirectory(BuildContext);
        if (directory != null)
        {
            return System.IO.Path.Combine(directory, "x64", "makeappx.exe");
        }

        return null;
    }
}