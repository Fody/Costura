//-------------------------------------------------------------

public class InnoSetupInstaller : IInstaller
{
    public InnoSetupInstaller(BuildContext buildContext)
    {
        BuildContext = buildContext;

        IsEnabled = BuildContext.BuildServer.GetVariableAsBool("InnoSetupEnabled", true, showValue: true);

        if (IsEnabled)
        {
            // In the future, check if InnoSetup is installed. Log error if not
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
            BuildContext.CakeContext.Information("Inno Setup is not enabled or available, skipping integration");
            return;
        }

        var innoSetupTemplateDirectory = System.IO.Path.Combine(".", "deployment", "innosetup", projectName);
        if (!BuildContext.CakeContext.DirectoryExists(innoSetupTemplateDirectory))
        {
            BuildContext.CakeContext.Information($"Skip packaging of app '{projectName}' using Inno Setup since no Inno Setup template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator($"Packaging app '{projectName}' using Inno Setup");

        var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);

        var installersOnDeploymentsShare = System.IO.Path.Combine(deploymentShare, "installer");
        BuildContext.CakeContext.CreateDirectory(installersOnDeploymentsShare);

        var setupSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();

        var innoSetupOutputRoot = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "innosetup", projectName);
        var innoSetupReleasesRoot = System.IO.Path.Combine(innoSetupOutputRoot, "releases");
        var innoSetupOutputIntermediate = System.IO.Path.Combine(innoSetupOutputRoot, "intermediate");

        BuildContext.CakeContext.CreateDirectory(innoSetupReleasesRoot);
        BuildContext.CakeContext.CreateDirectory(innoSetupOutputIntermediate);

        // Copy all files to the intermediate directory so Inno Setup knows what to do
        var appSourceDirectory = string.Format("{0}/{1}/**/*", BuildContext.General.OutputRootDirectory, projectName);
        var appTargetDirectory = innoSetupOutputIntermediate;

        BuildContext.CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        BuildContext.CakeContext.CopyFiles(appSourceDirectory, appTargetDirectory, true);

        // Set up InnoSetup template
        BuildContext.CakeContext.CopyDirectory(innoSetupTemplateDirectory, innoSetupOutputIntermediate);

        var innoSetupScriptFileName = System.IO.Path.Combine(innoSetupOutputIntermediate, "setup.iss");
        var fileContents = System.IO.File.ReadAllText(innoSetupScriptFileName);
        fileContents = fileContents.Replace("[CHANNEL_SUFFIX]", setupSuffix);
        fileContents = fileContents.Replace("[CHANNEL]", BuildContext.Installer.GetDeploymentChannelSuffix(" (", ")"));
        fileContents = fileContents.Replace("[VERSION]", BuildContext.General.Version.MajorMinorPatch);
        fileContents = fileContents.Replace("[VERSION_DISPLAY]", BuildContext.General.Version.FullSemVer);
        fileContents = fileContents.Replace("[WIZARDIMAGEFILE]", string.Format("logo_large{0}", setupSuffix));

        var signToolIndex = GetRandomSignToolIndex();

        try
        {
            var codeSignContext = BuildContext.General.CodeSign;
            var azureCodeSignContext = BuildContext.General.AzureCodeSign;
            
            var signTool = string.Empty;

            var signToolFileName = GetSignToolFileName(BuildContext);
            if (!string.IsNullOrWhiteSpace(signToolFileName))
            {
                var signToolName = DateTime.Now.ToString("yyyyMMddHHmmss");
                var signToolCommandLine = GetSignToolCommandLine(BuildContext);

                BuildContext.CakeContext.Information("Adding random sign tool config for Inno Setup");

                using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(GetRegistryKey(), true))
                {
                    var registryValueName = GetSignToolIndexName(signToolIndex);

                    // Important: must end with "$f"
                    var signToolRegistryValue = $"{signToolName}=\"{signToolFileName}\" {signToolCommandLine} \"$f\"";

                    registryKey.SetValue(registryValueName, signToolRegistryValue);
                }

                signTool = string.Format("SignTool={0}", signToolName);
            }

            fileContents = fileContents.Replace("[SIGNTOOL]", signTool);
            System.IO.File.WriteAllText(innoSetupScriptFileName, fileContents);

            BuildContext.CakeContext.Information("Generating Inno Setup packages, this can take a while, especially when signing is enabled...");

            BuildContext.CakeContext.InnoSetup(innoSetupScriptFileName, new InnoSetupSettings
            {
                OutputDirectory = innoSetupReleasesRoot
            });

            if (BuildContext.Wpf.UpdateDeploymentsShare)
            {
                BuildContext.CakeContext.Information("Copying Inno Setup files to deployments share at '{0}'", installersOnDeploymentsShare);

                // Copy the following files:
                // - Setup.exe => [projectName]-[version].exe
                // - Setup.exe => [projectName]-[channel].exe

                var installerSourceFile = System.IO.Path.Combine(innoSetupReleasesRoot, $"{projectName}_{BuildContext.General.Version.FullSemVer}.exe");
                BuildContext.CakeContext.CopyFile(installerSourceFile, System.IO.Path.Combine(installersOnDeploymentsShare, $"{projectName}_{BuildContext.General.Version.FullSemVer}.exe"));
                BuildContext.CakeContext.CopyFile(installerSourceFile, System.IO.Path.Combine(installersOnDeploymentsShare, $"{projectName}{setupSuffix}.exe"));
            }
        }
        finally
        {
            BuildContext.CakeContext.Information("Removing random sign tool config for Inno Setup");

            RemoveSignToolFromRegistry(signToolIndex);
        }
    }
    
    //-------------------------------------------------------------

    public async Task<DeploymentTarget> GenerateDeploymentTargetAsync(string projectName)
    {
        var deploymentTarget = new DeploymentTarget
        {
            Name = "Inno Setup"
        };

        var channels = new [] 
        {
            "alpha",
            "beta",
            "stable"
        };

        var deploymentGroupNames = new List<string>();
        var projectDeploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);

        // Just a single group
        deploymentGroupNames.Add("all");

        foreach (var deploymentGroupName in deploymentGroupNames)
        {
            BuildContext.CakeContext.Information($"Searching for releases for deployment group '{deploymentGroupName}'");

            var deploymentGroup = new DeploymentGroup
            {
                Name = deploymentGroupName
            };

            foreach (var channel in channels)
            {
                BuildContext.CakeContext.Information($"Searching for releases for deployment channel '{deploymentGroupName}/{channel}'");

                var deploymentChannel = new DeploymentChannel
                {
                    Name = channel
                };

                var targetDirectory = GetDeploymentsShareRootDirectory(projectName, channel);

                BuildContext.CakeContext.Information($"Searching for release files in '{targetDirectory}'");

                var filter = $"{projectName}_*{channel}*.exe";
                if (channel == "stable")
                {
                    filter = $"{projectName}_*.exe";
                }

                var installationFiles = System.IO.Directory.GetFiles(targetDirectory, filter);

                foreach (var installationFile in installationFiles)
                {
                    var releaseFileInfo = new System.IO.FileInfo(installationFile);
                    var relativeFileName = new DirectoryPath(projectDeploymentShare).GetRelativePath(new FilePath(releaseFileInfo.FullName)).FullPath.Replace("\\", "/");

                    var releaseVersion = releaseFileInfo.Name
                        .Replace($"{projectName}", string.Empty)
                        .Replace($".exe", string.Empty)
                        .Trim('_');

                    // Either empty or matching a release channel should be ignored
                    if (string.IsNullOrWhiteSpace(releaseVersion) ||
                        channels.Any(x => x == releaseVersion))
                    {
                        BuildContext.CakeContext.Information($"Ignoring '{installationFile}'");
                        continue;
                    }

                    // Special case for stable releases
                    if (channel == "stable")
                    {
                        if (releaseVersion.Contains("-alpha") ||
                            releaseVersion.Contains("-beta"))
                        {
                            BuildContext.CakeContext.Information($"Ignoring '{installationFile}'");
                            continue;
                        }
                    }

                    BuildContext.CakeContext.Information($"Applying release based on '{installationFile}'");

                    var release = new DeploymentRelease
                    {
                        Name = releaseVersion,
                        Timestamp = releaseFileInfo.CreationTimeUtc
                    };

                    // Full release
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
        var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);

        var installersOnDeploymentsShare = System.IO.Path.Combine(deploymentShare, "installer");
        BuildContext.CakeContext.CreateDirectory(installersOnDeploymentsShare);

        return installersOnDeploymentsShare;
    }

    //-------------------------------------------------------------
      
    private string GetRegistryKey()
    {
        return "Software\\Jordan Russell\\Inno Setup\\SignTools";
    }

    //-------------------------------------------------------------
      
    private int GetRandomSignToolIndex()
    {
        using (var registryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(GetRegistryKey()))
        {
            for (int i = 0; i < 100; i++)
            {
                var valueName = GetSignToolIndexName(i);

                if (registryKey.GetValue(valueName) is null)
                {
                    // Immediately lock it
                    registryKey.SetValue(valueName, "reserved");

                    return i;
                }
            }
        }
        
        throw new Exception("Could not find any empty slots for the sign tool, please clean up the sign tool registry for Inno Setup");
    }

    //-------------------------------------------------------------

    private string GetSignToolIndexName(int index)
    {
        return $"SignTool{index}";
    }

    //-------------------------------------------------------------

    private void RemoveSignToolFromRegistry(int index)
    {
        using (var registryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(GetRegistryKey()))
        {                
            var valueName = GetSignToolIndexName(index);

            registryKey.DeleteValue(valueName, false);
        }
    }
}
