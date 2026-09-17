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

        var deploymentDirectory = BuildContext.Wpf.GetDeploymentDirectoryForProject(BuildContext, projectName);
        var installersOnDeploymentsDirectory = System.IO.Path.Combine(deploymentDirectory, channel);
        BuildContext.CakeContext.CreateDirectory(installersOnDeploymentsDirectory);

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

        BuildContext.CakeContext.Information("Copying MSIX files to deployments directory at '{0}'", installersOnDeploymentsDirectory);

        // Copy the following files:
        // - [ProjectName]_[version].msix => [projectName]_[version].msix
        // - [ProjectName]_[version].msix => [projectName]_[channel].msix

        BuildContext.CakeContext.CopyFile(installerSourceFile, System.IO.Path.Combine(installersOnDeploymentsDirectory, msixInstallerName));
        BuildContext.CakeContext.CopyFile(installerSourceFile, System.IO.Path.Combine(installersOnDeploymentsDirectory, $"{projectName}{setupSuffix}.msix"));

        if (BuildContext.CakeContext.FileExists(msixUpdateScriptFileName))
        {
            // - App.AppInstaller => [projectName].AppInstaller
            BuildContext.CakeContext.CopyFile(msixUpdateScriptFileName, System.IO.Path.Combine(installersOnDeploymentsDirectory, $"{projectName}.AppInstaller"));
        }
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