#tool "dotnet:?package=vpk&version=1.2.0"

//-------------------------------------------------------------

public class VelopackInstaller : IInstaller
{
    public VelopackInstaller(BuildContext buildContext)
    {
        BuildContext = buildContext;

        IsEnabled = BuildContext.BuildServer.GetVariableAsBool("VelopackEnabled", false, showValue: true);
        UpdateUrl = BuildContext.BuildServer.GetVariable("VelopackUpdateUrl", showValue: true);

        if (IsEnabled)
        {
            IsAvailable = IsEnabled;
        }
    }

    public BuildContext BuildContext { get; private set; }

    public bool IsEnabled { get; private set; }

    public string UpdateUrl { get; private set; }

    public bool IsAvailable { get; private set; }

    //-------------------------------------------------------------

    public async Task PackageAsync(string projectName, string channel)
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("Velopack is not enabled or available, skipping integration");
            return;
        }

    	var vpkToolExe = BuildContext.CakeContext.Tools.Resolve("vpk.exe");

        // There are 2 flavors:
        //
        // 1: Non-grouped:              /[app]/[channel] (e.g. /MyApp/alpha)
        // Updates will always be applied, even to new major versions
        //
        // 2: Grouped by major version: /[app]/[major_version]/[channel] (e.g. /MyApp/4/alpha)
        // Updates will only be applied to non-major updates. This allows manual migration to
        // new major versions, which is very useful when there are dependencies that need to
        // be updated before a new major version can be switched to.
        var currentReleaseUrl = UpdateUrl;

        var velopackOutputRoot = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "velopack", projectName);
        if (!currentReleaseUrl.EndsWith("/"))
        {
            currentReleaseUrl += "/";
        }

        currentReleaseUrl += $"{projectName}";

        if (BuildContext.Wpf.GroupUpdatesByMajorVersion)
        {
            velopackOutputRoot = System.IO.Path.Combine(velopackOutputRoot, BuildContext.General.Version.Major);
            currentReleaseUrl += $"/{BuildContext.General.Version.Major}";
        }

        velopackOutputRoot = System.IO.Path.Combine(velopackOutputRoot, channel);
        currentReleaseUrl += $"/{channel}";

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

        var releasesTargetDirectory = velopackReleasesRoot;

        BuildContext.CakeContext.CreateDirectory(releasesTargetDirectory);

        // Download latest release via vpk download to generate delta updates
        if (string.IsNullOrWhiteSpace(UpdateUrl))
        {
            BuildContext.CakeContext.Warning($"Skipping delta update generation since base release URL is not specified");            
        }
        else
        {
            currentReleaseUrl = $"{currentReleaseUrl.ToLower()}";

            BuildContext.CakeContext.Information($"Downloading current release from '{currentReleaseUrl}'");   
            
            // Download via Velopack CLI
            var downloadArgumentBuilder = new ProcessArgumentBuilder()
                .Append("download")
                .Append("http")
                .Append("--verbose")
                .AppendSwitch("--channel", "win") // default channel since we manage channels ourselves
                .AppendSwitch("--url", currentReleaseUrl)
                .AppendSwitch("--outputDir", velopackReleasesRoot);

            BuildContext.CakeContext.StartProcess(vpkToolExe, new ProcessSettings
            {
                Arguments = downloadArgumentBuilder
            });
        }
        
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

        var deploymentDirectory = BuildContext.Wpf.GetDeploymentDirectoryForProject(BuildContext, projectName);
        System.IO.Directory.CreateDirectory(deploymentDirectory);

        BuildContext.CakeContext.Information($"Copying updated Velopack files back final deployments directory at '{deploymentDirectory}'");

        // Copy the following files:
        // - [version]-delta.nupkg
        // - [version]-full.nupkg
        // - Setup.exe => Setup.exe & WpfApp.exe
        // - releases.win.json
        // - RELEASES

        // Note to consider in future: this stores (and uploads) the same file 4 times. Maybe we need to stop processing so many files
        // to save time on uploads (and eventually money on storage)
        var velopackFiles = BuildContext.CakeContext.GetFiles($"{velopackReleasesRoot}/{appId}-{BuildContext.General.Version.NuGet}*.nupkg");
        BuildContext.CakeContext.CopyFiles(velopackFiles, deploymentDirectory);
        BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, $"{appId}-win-Portable.zip"), System.IO.Path.Combine(deploymentDirectory, $"{appId}-win-Portable.zip"));
        BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, $"{appId}-win-Setup.exe"), System.IO.Path.Combine(deploymentDirectory, $"{appId}-win-Setup.exe"));
        BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "Setup.exe"), System.IO.Path.Combine(deploymentDirectory, "Setup.exe"));
        BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "Setup.exe"), System.IO.Path.Combine(deploymentDirectory, $"{projectName}.exe"));
        BuildContext.CakeContext.CopyFile(System.IO.Path.Combine(velopackReleasesRoot, "releases.win.json"), System.IO.Path.Combine(deploymentDirectory, "releases.win.json"));
    }
}