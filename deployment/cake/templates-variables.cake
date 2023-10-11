#l "buildserver.cake"

//-------------------------------------------------------------

public class TemplatesContext : BuildContextWithItemsBase
{
    public TemplatesContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' template items");
    }
}

//-------------------------------------------------------------

private TemplatesContext InitializeTemplatesContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new TemplatesContext(parentBuildContext)
    {
        Items = Templates ?? new List<string>(),
    };

    return data;
}

//-------------------------------------------------------------

List<string> _templates;

public List<string> Templates
{
    get 
    {
        if (_templates is null)
        {
            _templates = new List<string>();
        }

        return _templates;
    }
}
