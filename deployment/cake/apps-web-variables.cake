#l "./buildserver.cake"

//-------------------------------------------------------------

public class WebContext : BuildContextWithItemsBase
{
    public WebContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    protected override void ValidateContext()
    {
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' web projects");
    }
}

//-------------------------------------------------------------

private WebContext InitializeWebContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new WebContext(parentBuildContext)
    {
        Items = WebApps ?? new List<string>()
    };

    return data;
}

//-------------------------------------------------------------

List<string> _webApps;

public List<string> WebApps
{
    get 
    {
        if (_webApps is null)
        {
            _webApps = new List<string>();
        }

        return _webApps;
    }
}