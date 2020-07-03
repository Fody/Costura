#pragma warning disable 1998

#l "apps-uwp-variables.cake"

#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=Microsoft.Azure.KeyVault.Core&version=1.0.0"
#addin "nuget:?package=WindowsAzure.Storage&version=9.1.1"
#addin "nuget:?package=Cake.WindowsAppStore&version=1.4.0"

//-------------------------------------------------------------

public class UwpProcessor : ProcessorBase
{
    public UwpProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }

    public override bool HasItems()
    {
        return BuildContext.Uwp.Items.Count > 0;
    }

    private void UpdateAppxManifestVersion(string path, string version)
    {
        CakeContext.Information("Updating AppxManifest version @ '{0}' to '{1}'", path, version);

        CakeContext.TransformConfig(path,
            new TransformationCollection {
                { "Package/Identity/@Version", version }
            });
    }

    private string GetArtifactsDirectory(string outputRootDirectory)
    {
        // 1 directory up since we want to turn "/output/release" into "/output/"
        var artifactsDirectoryString = System.IO.Path.Combine(outputRootDirectory, "..");
        var artifactsDirectory = CakeContext.MakeAbsolute(CakeContext.Directory(artifactsDirectoryString)).FullPath;

        return artifactsDirectory;
    }

    private string GetAppxUploadFileName(string artifactsDirectory, string solutionName, string versionMajorMinorPatch)
    {
        var appxUploadSearchPattern = System.IO.Path.Combine(artifactsDirectory, string.Format("{0}_{1}.0_*.appxupload", solutionName, versionMajorMinorPatch));

        CakeContext.Information("Searching for appxupload using '{0}'", appxUploadSearchPattern);

        var filesToZip = CakeContext.GetFiles(appxUploadSearchPattern);

        CakeContext.Information("Found '{0}' files to upload", filesToZip.Count);

        var appxUploadFile = filesToZip.FirstOrDefault();
        if (appxUploadFile is null)
        {
            return null;
        }
        
        var appxUploadFileName = appxUploadFile.FullPath;
        return appxUploadFileName;
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var uwpApp in BuildContext.Uwp.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, uwpApp))
            {
                BuildContext.Uwp.Items.Remove(uwpApp);
            }
        }
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var uwpApp in BuildContext.Uwp.Items)
        {
            var appxManifestFile = System.IO.Path.Combine(".", "src", uwpApp, "Package.appxmanifest");
            UpdateAppxManifestVersion(appxManifestFile, string.Format("{0}.0", BuildContext.General.Version.MajorMinorPatch));
        }
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }

        var platforms = new Dictionary<string, PlatformTarget>();
        //platforms["AnyCPU"] = PlatformTarget.MSIL;
        platforms["x86"] = PlatformTarget.x86;
        platforms["x64"] = PlatformTarget.x64;
        platforms["arm"] = PlatformTarget.ARM;

        // Important note: we only have to build for ARM, it will auto-build x86 / x64 as well
        var platform = platforms.First(x => x.Key == "arm");
        
        foreach (var uwpApp in BuildContext.Uwp.Items)
        {
            CakeContext.Information("Building UWP app '{0}'", uwpApp);

            var artifactsDirectory = GetArtifactsDirectory(BuildContext.General.OutputRootDirectory);
            var appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, uwpApp, BuildContext.General.Version.MajorMinorPatch);

            // If already exists, skip for store upload debugging
            if (appxUploadFileName != null && CakeContext.FileExists(appxUploadFileName))
            {
                CakeContext.Information(string.Format("File '{0}' already exists, skipping build", appxUploadFileName));
                continue;
            }

            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = platform.Value
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, uwpApp);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // See https://docs.microsoft.com/en-us/windows/uwp/packaging/auto-build-package-uwp-apps for all the details
            //msBuildSettings.Properties["UseDotNetNativeToolchain"] = new List<string>(new [] { "false" });
            //msBuildSettings.Properties["UapAppxPackageBuildMode"] = new List<string>(new [] { "StoreUpload" });
            msBuildSettings.Properties["UapAppxPackageBuildMode"] = new List<string>(new [] { "CI" });
            msBuildSettings.Properties["AppxBundlePlatforms"] = new List<string>(new [] { string.Join("|", platforms.Keys) });
            msBuildSettings.Properties["AppxBundle"] = new List<string>(new [] { "Always" });
            msBuildSettings.Properties["AppxPackageDir"] = new List<string>(new [] { artifactsDirectory });

            CakeContext.Information("Building project for platform {0}, artifacts directory is '{1}'", platform.Key, artifactsDirectory);

            var projectFileName = GetProjectFileName(BuildContext, uwpApp);

            // Note: if csproj doesn't work, use SolutionFileName instead
            //var projectFileName = SolutionFileName;
            CakeContext.MSBuild(projectFileName, msBuildSettings);

            // Recalculate!
            appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, uwpApp, BuildContext.General.Version.MajorMinorPatch);
            if (appxUploadFileName is null)
            {
                throw new Exception(string.Format("Couldn't determine the appxupload file using base directory '{0}'", artifactsDirectory));
            }

            CakeContext.Information("Created appxupload file '{0}'", appxUploadFileName, artifactsDirectory);
        }
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        // No specific implementation required for now, build already wraps it up
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        foreach (var uwpApp in BuildContext.Uwp.Items)
        {
            if (!ShouldDeployProject(BuildContext, uwpApp))
            {
                CakeContext.Information("UWP app '{0}' should not be deployed", uwpApp);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying UWP app '{0}'", uwpApp);

            var artifactsDirectory = GetArtifactsDirectory(BuildContext.General.OutputRootDirectory);
            var appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, uwpApp, BuildContext.General.Version.MajorMinorPatch);

            CakeContext.Information("Creating Windows Store app submission");

            CakeContext.CreateWindowsStoreAppSubmission(appxUploadFileName, new WindowsStoreAppSubmissionSettings
            {
                ApplicationId = BuildContext.Uwp.WindowsStoreAppId,
                ClientId = BuildContext.Uwp.WindowsStoreClientId,
                ClientSecret = BuildContext.Uwp.WindowsStoreClientSecret,
                TenantId = BuildContext.Uwp.WindowsStoreTenantId
            });    

            await BuildContext.Notifications.NotifyAsync(uwpApp, string.Format("Deployed to store"), TargetType.UwpApp);
        }
    }

    public override async Task FinalizeAsync()
    {

    }
}