#l "buildserver.cake"

//-------------------------------------------------------------

public class DependenciesContext : BuildContextWithItemsBase
{
    public DependenciesContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' dependency projects");
    }
}

//-------------------------------------------------------------

private DependenciesContext InitializeDependenciesContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new DependenciesContext(parentBuildContext)
    {
        Items = Dependencies ?? new List<string>()
    };

    return data;
}

//-------------------------------------------------------------

List<string> _dependencies;

public List<string> Dependencies
{
    get 
    {
        if (_dependencies is null)
        {
            _dependencies = new List<string>();
        }

        return _dependencies;
    }
}