#l "buildserver.cake"

//-------------------------------------------------------------

public class ComponentsContext : BuildContextWithItemsBase
{
    public ComponentsContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string NuGetRepositoryUrl { get; set; }
    public string NuGetRepositoryApiKey { get; set; }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' component projects");
    }
}

//-------------------------------------------------------------

private ComponentsContext InitializeComponentsContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new ComponentsContext(parentBuildContext)
    {
        Items = Components ?? new List<string>(),
        NuGetRepositoryUrl = buildContext.BuildServer.GetVariable("NuGetRepositoryUrl", showValue: true),
        NuGetRepositoryApiKey = buildContext.BuildServer.GetVariable("NuGetRepositoryApiKey", showValue: false)
    };

    return data;
}

//-------------------------------------------------------------

List<string> _components;

public List<string> Components
{
    get 
    {
        if (_components is null)
        {
            _components = new List<string>();
        }

        return _components;
    }
}