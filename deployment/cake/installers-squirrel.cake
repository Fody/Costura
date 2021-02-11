#addin "nuget:?package=Cake.Squirrel&version=0.15.1"

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

    public async Task PackageAsync(string projectName, string channel)
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("Squirrel is not enabled or available, skipping integration");
            return;
        }

        var squirrelOutputRoot = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "squirrel", projectName, channel);
        var squirrelReleasesRoot = System.IO.Path.Combine(squirrelOutputRoot, "releases");
        var squirrelOutputIntermediate = System.IO.Path.Combine(squirrelOutputRoot, "intermediate");

        var nuSpecTemplateFileName = System.IO.Path.Combine(".", "deployment", "squirrel", "template", $"{projectName}.nuspec");
        var nuSpecFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectName}.nuspec");
        var nuGetFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectName}.{BuildContext.General.Version.NuGet}.nupkg");

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

        BuildContext.CakeContext.Information("Copying files from '{0}' => '{1}'", appSourceDirectory, appTargetDirectory);

        BuildContext.CakeContext.CopyDirectory(appSourceDirectory, appTargetDirectory);

        var squirrelSourceFile = BuildContext.CakeContext.GetFiles("./tools/squirrel.windows.*/tools/Squirrel.exe").Single();

        // We need to be 1 level deeper, let's just walk each directory in case we can support multi-platform releases
        // in the future
        foreach (var subDirectory in BuildContext.CakeContext.GetSubDirectories(appTargetDirectory))
        {
            var squirrelTargetFile = System.IO.Path.Combine(appTargetDirectory, subDirectory.Segments[subDirectory.Segments.Length - 1], "Squirrel.exe");

            BuildContext.CakeContext.Information("Copying Squirrel.exe to support self-updates from '{0}' => '{1}'", squirrelSourceFile, squirrelTargetFile);

            BuildContext.CakeContext.CopyFile(squirrelSourceFile, squirrelTargetFile);
        }

        // Make sure all files are signed before we package them for Squirrel (saves potential errors occurring later in squirrel releasify)
        var signToolCommand = string.Empty;

        if (!string.IsNullOrWhiteSpace(BuildContext.General.CodeSign.CertificateSubjectName))
        {
            signToolCommand = string.Format("/a /t {0} /n {1}", BuildContext.General.CodeSign.TimeStampUri, 
                BuildContext.General.CodeSign.CertificateSubjectName);
        }

        // Create NuGet package
        BuildContext.CakeContext.NuGetPack(nuSpecFileName, new NuGetPackSettings
        {
            OutputDirectory = squirrelOutputIntermediate,
        });

        // Rename so we have the right nuget package file names (without the channel)
        if (!string.IsNullOrWhiteSpace(setupSuffix))
        {
            var sourcePackageFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectSlug}{setupSuffix}.{BuildContext.General.Version.NuGet}.nupkg");
            var targetPackageFileName = System.IO.Path.Combine(squirrelOutputIntermediate, $"{projectName}.{BuildContext.General.Version.NuGet}.nupkg");

            BuildContext.CakeContext.Information("Moving file from '{0}' => '{1}'", sourcePackageFileName, targetPackageFileName);

            BuildContext.CakeContext.MoveFile(sourcePackageFileName, targetPackageFileName);
        }
        
        var deploymentShare = BuildContext.Wpf.GetDeploymentShareForProject(projectName);

        // Copy deployments share to the intermediate root so we can locally create the Squirrel releases
        var releasesSourceDirectory = System.IO.Path.Combine(deploymentShare, channel);
        var releasesTargetDirectory = squirrelReleasesRoot;

        BuildContext.CakeContext.Information("Copying releases from '{0}' => '{1}'", releasesSourceDirectory, releasesTargetDirectory);

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
            BuildContext.CakeContext.Information("Copying updated Squirrel files back to deployments share at '{0}'", releasesSourceDirectory);

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
}