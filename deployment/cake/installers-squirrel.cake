#addin "nuget:?package=Cake.Squirrel&version=0.13.0"

#tool "nuget:?package=Squirrel.Windows&version=1.9.1"

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

    public async Task PackageAsync(string projectName, string channel)
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("Squirrel is not enabled or available, skipping integration");
            return;
        }

        var squirrelOutputRoot = string.Format("{0}/squirrel/{1}/{2}", BuildContext.General.OutputRootDirectory, projectName, channel);
        var squirrelReleasesRoot = string.Format("{0}/releases", squirrelOutputRoot);
        var squirrelOutputIntermediate = string.Format("{0}/intermediate", squirrelOutputRoot);

        var nuSpecTemplateFileName = string.Format("./deployment/squirrel/template/{0}.nuspec", projectName);
        var nuSpecFileName = string.Format("{0}/{1}.nuspec", squirrelOutputIntermediate, projectName);
        var nuGetFileName = string.Format("{0}/{1}.{2}.nupkg", squirrelOutputIntermediate, projectName, BuildContext.General.Version.NuGet);

        if (!BuildContext.CakeContext.FileExists(nuSpecTemplateFileName))
        {
            BuildContext.CakeContext.Information("Skip packaging of WPF app '{0}' using Squirrel since no Squirrel template is present");
            return;
        }

        BuildContext.CakeContext.LogSeparator("Packaging WPF app '{0}' using Squirrel", projectName);

        BuildContext.CakeContext.CreateDirectory(squirrelReleasesRoot);
        BuildContext.CakeContext.CreateDirectory(squirrelOutputIntermediate);

        // Set up Squirrel nuspec
        BuildContext.CakeContext.CopyFile(nuSpecTemplateFileName, nuSpecFileName);

        var setupSuffix = BuildContext.Installer.GetDeploymentChannelSuffix();

        BuildContext.CakeContext.TransformConfig(nuSpecFileName,
            new TransformationCollection 
            {
                { "package/metadata/id", $"{projectName}{setupSuffix}" },
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
        var appSourceDirectory = string.Format("{0}/{1}", BuildContext.General.OutputRootDirectory, projectName);
        var appTargetDirectory = string.Format("{0}/lib", squirrelOutputIntermediate);

        BuildContext.CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        BuildContext.CakeContext.CopyDirectory(appSourceDirectory, appTargetDirectory);

        // Create NuGet package
        BuildContext.CakeContext.NuGetPack(nuSpecFileName, new NuGetPackSettings
        {
            OutputDirectory = squirrelOutputIntermediate,
        });

        // Rename so we have the right nuget package file names (without the channel)
        if (!string.IsNullOrWhiteSpace(setupSuffix))
        {
            var sourcePackageFileName = $"{squirrelOutputIntermediate}/{projectName}{setupSuffix}.{BuildContext.General.Version.NuGet}.nupkg";
            var targetPackageFileName = $"{squirrelOutputIntermediate}/{projectName}.{BuildContext.General.Version.NuGet}.nupkg";

            BuildContext.CakeContext.Information("Moving file from '{0}' => '{1}'", sourcePackageFileName, targetPackageFileName);

            BuildContext.CakeContext.MoveFile(sourcePackageFileName, targetPackageFileName);
        }
        
        // Copy deployments share to the intermediate root so we can locally create the Squirrel releases
        var releasesSourceDirectory = string.Format("{0}/{1}/{2}", BuildContext.Wpf.DeploymentsShare, projectName, channel);
        var releasesTargetDirectory = squirrelReleasesRoot;

        BuildContext.CakeContext.Information("Copying releases from '{0}' => '{1}'", releasesSourceDirectory, releasesTargetDirectory);

        BuildContext.CakeContext.CopyDirectory(releasesSourceDirectory, releasesTargetDirectory);

        // Squirrelify!
        var squirrelSettings = new SquirrelSettings();
        squirrelSettings.NoMsi = false;
        squirrelSettings.ReleaseDirectory = squirrelReleasesRoot;
        squirrelSettings.LoadingGif = "./deployment/squirrel/loading.gif";

        // Note: this is not really generic, but this is where we store our icons file, we can
        // always change this in the future
        var iconFileName = $"./design/logo/logo{setupSuffix}.ico";
        squirrelSettings.Icon = iconFileName;
        squirrelSettings.SetupIcon = iconFileName;

        if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
        {
            squirrelSettings.SigningParameters = string.Format("/a /t {0} /n {1}", BuildContext.General.CodeSign.TimeStampUri, 
                BuildContext.General.CodeSign.CertificateSubjectName);
        }

        BuildContext.CakeContext.Information("Generating Squirrel packages, this can take a while, especially when signing is enabled...");

        BuildContext.CakeContext.Squirrel(nuGetFileName, squirrelSettings);

        if (BuildContext.Wpf.UpdateDeploymentsShare)
        {
            BuildContext.CakeContext.Information("Copying updated Squirrel files back to deployments share at '{0}'", releasesSourceDirectory);

            // Copy the following files:
            // - [version]-full.nupkg
            // - [version]-full.nupkg
            // - Setup.exe => Setup.exe & WpfApp.exe
            // - Setup.msi
            // - RELEASES

            var squirrelFiles = BuildContext.CakeContext.GetFiles($"{squirrelReleasesRoot}/{projectName}{setupSuffix}-{BuildContext.General.Version.NuGet}*.nupkg");
            BuildContext.CakeContext.CopyFiles(squirrelFiles, releasesSourceDirectory);
            BuildContext.CakeContext.CopyFile(string.Format("{0}/Setup.exe", squirrelReleasesRoot), string.Format("{0}/Setup.exe", releasesSourceDirectory));
            BuildContext.CakeContext.CopyFile(string.Format("{0}/Setup.exe", squirrelReleasesRoot), string.Format("{0}/{1}.exe", releasesSourceDirectory, projectName));
            BuildContext.CakeContext.CopyFile(string.Format("{0}/Setup.msi", squirrelReleasesRoot), string.Format("{0}/Setup.msi", releasesSourceDirectory));
            BuildContext.CakeContext.CopyFile(string.Format("{0}/RELEASES", squirrelReleasesRoot), string.Format("{0}/RELEASES", releasesSourceDirectory));
        }
    }
}