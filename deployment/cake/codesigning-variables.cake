#l "buildserver.cake"

//-------------------------------------------------------------

public class CodeSigningContext : BuildContextBase
{
    public CodeSigningContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public List<string> ProjectsToSignImmediately { get; set; }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        //CakeContext.Information($"Found '{Items.Count}' component projects");
    }
}

//-------------------------------------------------------------

private CodeSigningContext InitializeCodeSigningContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new CodeSigningContext(parentBuildContext)
    {
        ProjectsToSignImmediately = CodeSignImmediately,
    };

    return data;
}

//-------------------------------------------------------------

List<string> _codeSignImmediately;

public List<string> CodeSignImmediately
{
    get 
    {
        if (_codeSignImmediately is null)
        {
            _codeSignImmediately = new List<string>();
        }

        return _codeSignImmediately;
    }
}