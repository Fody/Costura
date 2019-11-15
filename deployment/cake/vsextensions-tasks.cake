#l "vsextensions-variables.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"

using System.Xml.Linq;

//-------------------------------------------------------------

public class VsExtensionsProcessor : ProcessorBase
{
    public VsExtensionsProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        
    }

    public override bool HasItems()
    {
        return BuildContext.VsExtensions.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var vsExtension in BuildContext.VsExtensions.Items.ToList())
        {
            if (!ShouldProcessProject(BuildContext, vsExtension))
            {
                BuildContext.VsExtensions.Items.Remove(vsExtension);
            }
        }        
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Note: since we can't use prerelease tags in VSIX, we will use the commit count
        // as last part of the version
        var version = string.Format("{0}.{1}", BuildContext.General.Version.MajorMinorPatch, BuildContext.General.Version.CommitsSinceVersionSource);

        foreach (var vsExtension in BuildContext.VsExtensions.Items)
        {
            CakeContext.Information("Updating version for vs extension '{0}'", vsExtension);

            var projectDirectory = GetProjectDirectory(vsExtension);

            // Step 1: update vsix manifest
            var vsixManifestFileName = string.Format("{0}\\source.extension.vsixmanifest", projectDirectory);

            CakeContext.TransformConfig(vsixManifestFileName, new TransformationCollection 
            {
                { "PackageManifest/Metadata/Identity/@Version", version },
                { "PackageManifest/Metadata/Identity/@Publisher", BuildContext.VsExtensions.PublisherName }
            });
        }        
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }
        
        foreach (var vsExtension in BuildContext.VsExtensions.Items)
        {
            BuildContext.CakeContext.LogSeparator("Building vs extension '{0}'", vsExtension);

            var projectFileName = GetProjectFileName(BuildContext, vsExtension);
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = BuildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(BuildContext, msBuildSettings, vsExtension);
            
            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(BuildContext, vsExtension);
            CakeContext.Information("Output directory: '{0}'", outputDirectory);

            // Since vs extensions (for now) use the old csproj style, make sure
            // to override the output path as well
            // msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            // msBuildSettings.WithProperty("PackageOutputPath", OutputRootDirectory);
            msBuildSettings.WithProperty("OutputPath", outputDirectory);

            CakeContext.MSBuild(projectFileName, msBuildSettings);
        }       
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // No packaging required        
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        var vsixPublisherExeDirectory = string.Format(@"{0}\VSSDK\VisualStudioIntegration\Tools\Bin", GetVisualStudioDirectory(BuildContext));
        var vsixPublisherExeFileName = string.Format(@"{0}\VsixPublisher.exe", vsixPublisherExeDirectory);

        foreach (var vsExtension in BuildContext.VsExtensions.Items)
        {
            if (!ShouldDeployProject(BuildContext, vsExtension))
            {
                CakeContext.Information("Vs extension '{0}' should not be deployed", vsExtension);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying vs extension '{0}'", vsExtension);

            // Step 1: copy the output stuff
            var vsExtensionOutputDirectory = GetProjectOutputDirectory(BuildContext, vsExtension);
            var payloadFileName = string.Format(@"{0}\{1}.vsix", vsExtensionOutputDirectory, vsExtension);

            var overviewSourceFileName = string.Format(@"src\{0}\overview.md", vsExtension);
            var overviewTargetFileName = string.Format(@"{0}\overview.md", vsExtensionOutputDirectory);
            CakeContext.CopyFile(overviewSourceFileName, overviewTargetFileName);

            var vsGalleryManifestSourceFileName = string.Format(@"src\{0}\source.extension.vsgallerymanifest", vsExtension);
            var vsGalleryManifestTargetFileName = string.Format(@"{0}\source.extension.vsgallerymanifest", vsExtensionOutputDirectory);
            CakeContext.CopyFile(vsGalleryManifestSourceFileName, vsGalleryManifestTargetFileName);

            // Step 2: update vs gallery manifest
            var fileContents = System.IO.File.ReadAllText(vsGalleryManifestTargetFileName);

            fileContents = fileContents.Replace("[PUBLISHERNAME]", BuildContext.VsExtensions.PublisherName);

            System.IO.File.WriteAllText(vsGalleryManifestTargetFileName, fileContents);

            // Step 3: go ahead and publish
            CakeContext.StartProcess(vsixPublisherExeFileName, new ProcessSettings 
            {
                Arguments = new ProcessArgumentBuilder()
                    .Append("publish")
                    .AppendSwitch("-payload", payloadFileName)
                    .AppendSwitch("-publishManifest", vsGalleryManifestTargetFileName)
                    .AppendSwitchSecret("-personalAccessToken", BuildContext.VsExtensions.PersonalAccessToken)
            });

            await BuildContext.Notifications.NotifyAsync(vsExtension, string.Format("Deployed to Visual Studio Gallery"), TargetType.VsExtension);
        }        
    }

    public override async Task FinalizeAsync()
    {

    }
}
