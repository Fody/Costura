#l "buildserver.cake"

//-------------------------------------------------------------

public class ToolsContext : BuildContextWithItemsBase
{
    public ToolsContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string NuGetRepositoryUrls { get; set; }
    public string NuGetRepositoryApiKeys { get; set; }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' tool projects");
    }
}

//-------------------------------------------------------------

private ToolsContext InitializeToolsContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new ToolsContext(parentBuildContext)
    {
        Items = Tools ?? new List<string>(),
        NuGetRepositoryUrls = buildContext.BuildServer.GetVariable("ToolsNuGetRepositoryUrls", showValue: true),
        NuGetRepositoryApiKeys = buildContext.BuildServer.GetVariable("ToolsNuGetRepositoryApiKeys", showValue: false)
    };

    return data;
}

//-------------------------------------------------------------

List<string> _tools;

public List<string> Tools
{
    get 
    {
        if (_tools is null)
        {
            _tools = new List<string>();
        }

        return _tools;
    }
}