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

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' wpf projects");
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
        AzureDeploymentsStorageConnectionString = buildContext.BuildServer.GetVariable("AzureDeploymentsStorageConnectionString")
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