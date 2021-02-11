#l "buildserver.cake"

public class VsExtensionsContext : BuildContextWithItemsBase
{
    public VsExtensionsContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }    

    public string PublisherName { get; set; }
    public string PersonalAccessToken { get; set; }

    protected override void ValidateContext()
    {
    
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' vs extension projects");
    }
}

//-------------------------------------------------------------

private VsExtensionsContext InitializeVsExtensionsContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new VsExtensionsContext(parentBuildContext)
    {
        Items = VsExtensions ?? new List<string>(),
        PublisherName = buildContext.BuildServer.GetVariable("VsExtensionsPublisherName", showValue: true),
        PersonalAccessToken = buildContext.BuildServer.GetVariable("VsExtensionsPersonalAccessToken", showValue: false),
    };

    return data;
}

//-------------------------------------------------------------

List<string> _vsExtensions;

public List<string> VsExtensions
{
    get 
    {
        if (_vsExtensions is null)
        {
            _vsExtensions = new List<string>();
        }

        return _vsExtensions;
    }
}