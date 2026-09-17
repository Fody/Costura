#l "buildserver.cake"

//-------------------------------------------------------------

public class WpfContext : BuildContextWithItemsBase
{
    public WpfContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }


    public string Channel { get; set; }
    public bool AppendDeploymentChannelSuffix { get; set; }
    public string AzureDeploymentsStorageConnectionString { get; set; }

    public bool GroupUpdatesByMajorVersion { get; set; }
    public bool DeployUpdatesToAlphaChannel { get; set; }
    public bool DeployUpdatesToBetaChannel { get; set; }
    public bool DeployUpdatesToStableChannel { get; set; }
    public bool DeployInstallers { get; set; }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' wpf projects");

        CakeContext.Information($"Group updates by major version: '{GroupUpdatesByMajorVersion}'");
        CakeContext.Information($"Deploy updates to alpha channel: '{DeployUpdatesToAlphaChannel}'");
        CakeContext.Information($"Deploy updates to beta channel: '{DeployUpdatesToBetaChannel}'");
        CakeContext.Information($"Deploy updates to stable channel: '{DeployUpdatesToStableChannel}'");
        CakeContext.Information($"Deploy installers: '{DeployInstallers}'");
    }

    public string GetDeploymentDirectoryForProject(BuildContext buildContext, string projectName)
    {
        var projectSlug = GetProjectSlug(projectName, "-");
        var deploymentDirectory = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, "AppDeployment", projectSlug);

        return deploymentDirectory;
    }
}

//-------------------------------------------------------------

private WpfContext InitializeWpfContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new WpfContext(parentBuildContext)
    {
        Items = WpfApps ?? new List<string>(),
        Channel = buildContext.BuildServer.GetVariable("Channel", showValue: true),
        AppendDeploymentChannelSuffix = buildContext.BuildServer.GetVariableAsBool("AppendDeploymentChannelSuffix", false, showValue: true),
        AzureDeploymentsStorageConnectionString = buildContext.BuildServer.GetVariable("AzureDeploymentsStorageConnectionString"),
        GroupUpdatesByMajorVersion = buildContext.BuildServer.GetVariableAsBool("WpfGroupUpdatesByMajorVersion", false, showValue: true),
        DeployUpdatesToAlphaChannel = buildContext.BuildServer.GetVariableAsBool("WpfDeployUpdatesToAlphaChannel", true, showValue: true),
        DeployUpdatesToBetaChannel = buildContext.BuildServer.GetVariableAsBool("WpfDeployUpdatesToBetaChannel", true, showValue: true),
        DeployUpdatesToStableChannel = buildContext.BuildServer.GetVariableAsBool("WpfDeployUpdatesToStableChannel", true, showValue: true),
        DeployInstallers = buildContext.BuildServer.GetVariableAsBool("WpfDeployInstallers", true, showValue: true),
    };

    if (string.IsNullOrWhiteSpace(data.Channel))
    {
        data.Channel = DetermineChannel(buildContext.General);

        data.CakeContext.Information($"Determined channel '{data.Channel}' for wpf projects");
    }

    return data;
}

//-------------------------------------------------------------

List<string> _wpfApps;

public List<string> WpfApps
{
    get 
    {
        if (_wpfApps is null)
        {
            _wpfApps = new List<string>();
        }

        return _wpfApps;
    }
}