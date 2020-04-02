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
    }
}