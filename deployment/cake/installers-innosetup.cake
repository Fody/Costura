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

    public async Task PackageAsync(string projectName, string channel)
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("Inno Setup is not enabled or available, skipping integration");
            return;
        }

        var innoSetupTemplateDirectory = string.Format("./deployment/innosetup/{0}", projectName);
        if (!BuildContext.CakeContext.DirectoryExists(innoSetupTemplateDirectory))
        {
            BuildContext.CakeContext.Information("Skip packaging of app '{0}' using Inno Setup since no Inno Setup template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator("Packaging app '{0}' using Inno Setup", projectName);

        var installersOnDeploymentsShare = string.Format("{0}/{1}/installer", BuildContext.Wpf.DeploymentsShare, projectName);
        BuildContext.CakeContext.CreateDirectory(installersOnDeploymentsShare);

        var setupSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();

        var innoSetupOutputRoot = string.Format("{0}/innosetup/{1}", BuildContext.General.OutputRootDirectory, projectName);
        var innoSetupReleasesRoot = string.Format("{0}/releases", innoSetupOutputRoot);
        var innoSetupOutputIntermediate = string.Format("{0}/intermediate", innoSetupOutputRoot);

        BuildContext.CakeContext.CreateDirectory(innoSetupReleasesRoot);
        BuildContext.CakeContext.CreateDirectory(innoSetupOutputIntermediate);

        // Set up InnoSetup template
        BuildContext.CakeContext.CopyDirectory(innoSetupTemplateDirectory, innoSetupOutputIntermediate);

        var innoSetupScriptFileName = string.Format("{0}/setup.iss", innoSetupOutputIntermediate);
        var fileContents = System.IO.File.ReadAllText(innoSetupScriptFileName);
        fileContents = fileContents.Replace("[CHANNEL_SUFFIX]", setupSuffix);
        fileContents = fileContents.Replace("[CHANNEL]", BuildContext.Installer.GetDeploymentChannelSuffix(" (", ")"));
        fileContents = fileContents.Replace("[VERSION]", BuildContext.General.Version.MajorMinorPatch);
        fileContents = fileContents.Replace("[VERSION_DISPLAY]", BuildContext.General.Version.FullSemVer);
        fileContents = fileContents.Replace("[WIZARDIMAGEFILE]", string.Format("logo_large{0}", setupSuffix));

        var signTool = string.Empty;
        if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
        {
            signTool = string.Format("SignTool={0}", BuildContext.General.CodeSign.CertificateSubjectName);
        }

        fileContents = fileContents.Replace("[SIGNTOOL]", signTool);
        System.IO.File.WriteAllText(innoSetupScriptFileName, fileContents);

        // Copy all files to the intermediate directory so Inno Setup knows what to do
        var appSourceDirectory = string.Format("{0}/{1}/**/*", BuildContext.General.OutputRootDirectory, projectName);
        var appTargetDirectory = innoSetupOutputIntermediate;

        BuildContext.CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        BuildContext.CakeContext.CopyFiles(appSourceDirectory, appTargetDirectory, true);

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

            var installerSourceFile = $"{innoSetupReleasesRoot}/{projectName}_{BuildContext.General.Version.FullSemVer}.exe";
            BuildContext.CakeContext.CopyFile(installerSourceFile, string.Format("{0}/{1}_{2}.exe", installersOnDeploymentsShare, projectName, BuildContext.General.Version.FullSemVer));
            BuildContext.CakeContext.CopyFile(installerSourceFile, string.Format("{0}/{1}{2}.exe", installersOnDeploymentsShare, projectName, setupSuffix));
        }
    }
}
