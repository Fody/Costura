// Customize this file when using a different issue tracker
#l "installers-innosetup.cake"
#l "installers-msix.cake"
#l "installers-squirrel.cake"

using System.Diagnostics;

//-------------------------------------------------------------

public interface IInstaller
{
    bool IsAvailable { get; }

    Task PackageAsync(string projectName, string channel);

    Task<DeploymentTarget> GenerateDeploymentTargetAsync(string projectName);
}

//-------------------------------------------------------------

public class DeploymentCatalog
{
    public DeploymentCatalog()
    {
        Targets = new List<DeploymentTarget>();
    }

    public List<DeploymentTarget> Targets { get; private set; }
}

//-------------------------------------------------------------

public class DeploymentTarget
{
    public DeploymentTarget()
    {
        Groups = new List<DeploymentGroup>();
    }

    public string Name { get; set; }

    public List<DeploymentGroup> Groups { get; private set; }
}

//-------------------------------------------------------------

public class DeploymentGroup
{
    public DeploymentGroup()
    {
        Channels = new List<DeploymentChannel>();
    }

    public string Name { get; set; }

    public List<DeploymentChannel> Channels { get; private set; }
}

//-------------------------------------------------------------

public class DeploymentChannel
{
    public DeploymentChannel()
    {
        Releases = new List<DeploymentRelease>();
    }

    public string Name { get; set; }

    public List<DeploymentRelease> Releases { get; private set; }
}

//-------------------------------------------------------------

public class DeploymentRelease
{
    public string Name { get; set; }

    public DateTime? Timestamp { get; set;}

    public bool HasFull
    {
        get { return Full is not null; }
    }

    public DeploymentReleasePart Full { get; set; }

    public bool HasDelta
    {
        get { return Delta is not null; }
    }

    public DeploymentReleasePart Delta { get; set; }
}

//-------------------------------------------------------------

public class DeploymentReleasePart
{
    public string Hash { get; set; }

    public string RelativeFileName { get; set; }

    public ulong Size { get; set; }
}

//-------------------------------------------------------------

public class InstallerIntegration : IntegrationBase
{
    private readonly List<IInstaller> _installers = new List<IInstaller>();

    public InstallerIntegration(BuildContext buildContext)
        : base(buildContext)
    {
        _installers.Add(new InnoSetupInstaller(buildContext));
        _installers.Add(new MsixInstaller(buildContext));
        _installers.Add(new SquirrelInstaller(buildContext));
    }

    public string GetDeploymentChannelSuffix(string prefix = "_", string suffix = "")
    {
        var channelSuffix = string.Empty;

        if (BuildContext.Wpf.AppendDeploymentChannelSuffix)
        {
            if (BuildContext.General.IsAlphaBuild ||
                BuildContext.General.IsBetaBuild)
            {
                channelSuffix = $"{prefix}{BuildContext.Wpf.Channel}{suffix}";
            }

            BuildContext.CakeContext.Information($"Using deployment channel suffix '{channelSuffix}'");
        }

        return channelSuffix; 
    }

    public async Task PackageAsync(string projectName, string channel)
    {
        BuildContext.CakeContext.LogSeparator($"Packaging installer for '{projectName}'");

        foreach (var installer in _installers)
        {
            if (!installer.IsAvailable)
            {
                continue;
            }

            BuildContext.CakeContext.LogSeparator($"Applying installer '{installer.GetType().Name}' for '{projectName}'");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await installer.PackageAsync(projectName, channel);
            }
            finally
            {
                stopwatch.Stop();

                BuildContext.CakeContext.Information($"Installer took {stopwatch.Elapsed}");
            }
        }

        if (BuildContext.Wpf.GenerateDeploymentCatalog)
        {
            BuildContext.CakeContext.LogSeparator($"Generating deployment catalog for '{projectName}'");

            var catalog = new DeploymentCatalog();

            foreach (var installer in _installers)
            {
                if (!installer.IsAvailable)
                {
                    continue;
                }

                BuildContext.CakeContext.LogSeparator($"Generating deployment target for catalog for installer '{installer.GetType().Name}' for '{projectName}'");
             
                var deploymentTarget = await installer.GenerateDeploymentTargetAsync(projectName);
                if (deploymentTarget is not null)
                {
                    catalog.Targets.Add(deploymentTarget);
                }
            }

            var localCatalogDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "catalog", projectName);
            BuildContext.CakeContext.CreateDirectory(localCatalogDirectory);

            var localCatalogFileName = System.IO.Path.Combine(localCatalogDirectory, "catalog.json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(catalog);

            System.IO.File.WriteAllText(localCatalogFileName, json);

            if (BuildContext.Wpf.UpdateDeploymentsShare)
            {
                var targetFileName = System.IO.Path.Combine(BuildContext.Wpf.GetDeploymentShareForProject(projectName), "catalog.json");
                BuildContext.CakeContext.CopyFile(localCatalogFileName, targetFileName);
            }
        }
    }
}