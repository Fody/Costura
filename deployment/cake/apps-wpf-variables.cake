#l "buildserver.cake"

//-------------------------------------------------------------

public class WpfContext : BuildContextWithItemsBase
{
    public WpfContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }


    public string DeploymentsShare { get; set; }
    public string Channel { get; set; }
    public bool AppendDeploymentChannelSuffix { get; set; }
    public bool UpdateDeploymentsShare { get; set; }
    public string AzureDeploymentsStorageConnectionString { get; set; }

    public bool GenerateDeploymentCatalog { get; set; }
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

        CakeContext.Information($"Generate Deployment Catalog: '{GenerateDeploymentCatalog}'");
        CakeContext.Information($"Group updates by major version: '{GroupUpdatesByMajorVersion}'");
        CakeContext.Information($"Deploy updates to alpha channel: '{DeployUpdatesToAlphaChannel}'");
        CakeContext.Information($"Deploy updates to beta channel: '{DeployUpdatesToBetaChannel}'");
        CakeContext.Information($"Deploy updates to stable channel: '{DeployUpdatesToStableChannel}'");
        CakeContext.Information($"Deploy installers: '{DeployInstallers}'");
    }

    public string GetDeploymentShareForProject(string projectName)
    {
        var projectSlug = GetProjectSlug(projectName, "-");
        var deploymentShare = System.IO.Path.Combine(DeploymentsShare, projectSlug);

        return deploymentShare;
    }
}

//-------------------------------------------------------------

private WpfContext InitializeWpfContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new WpfContext(parentBuildContext)
    {
        Items = WpfApps ?? new List<string>(),
        DeploymentsShare = buildContext.BuildServer.GetVariable("DeploymentsShare", showValue: true),
        Channel = buildContext.BuildServer.GetVariable("Channel", showValue: true),
        AppendDeploymentChannelSuffix = buildContext.BuildServer.GetVariableAsBool("AppendDeploymentChannelSuffix", false, showValue: true),
        UpdateDeploymentsShare = buildContext.BuildServer.GetVariableAsBool("UpdateDeploymentsShare", true, showValue: true),
        AzureDeploymentsStorageConnectionString = buildContext.BuildServer.GetVariable("AzureDeploymentsStorageConnectionString"),
        GenerateDeploymentCatalog = buildContext.BuildServer.GetVariableAsBool("WpfGenerateDeploymentCatalog", true, showValue: true),
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