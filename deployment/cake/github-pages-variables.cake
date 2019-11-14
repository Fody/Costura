#l "buildserver.cake"

//-------------------------------------------------------------

public class GitHubPagesContext : BuildContextWithItemsBase
{
    public GitHubPagesContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string RepositoryUrl { get; set; }
    public string BranchName { get; set; }
    public string Email { get; set; }
    public string UserName { get; set; }
    public string ApiToken { get; set; }

    protected override void ValidateContext()
    {
        if (Items.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RepositoryUrl))
        {
            throw new Exception("GitHubPagesRepositoryUrl must be defined");
        }

        if (string.IsNullOrWhiteSpace(BranchName))
        {
            throw new Exception("GitHubPagesBranchName must be defined");
        }
                    
        if (string.IsNullOrWhiteSpace(Email))
        {
            throw new Exception("GitHubPagesEmail must be defined");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new Exception("GitHubPagesUserName must be defined");
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            throw new Exception("GitHubPagesApiToken must be defined");
        }        
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' GitHub pages projects");
    }
}

//-------------------------------------------------------------

private GitHubPagesContext InitializeGitHubPagesContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new GitHubPagesContext(parentBuildContext)
    {
        Items = GitHubPages ?? new List<string>(),
        RepositoryUrl = buildContext.BuildServer.GetVariable("GitHubPagesRepositoryUrl", ((BuildContext)parentBuildContext).General.Repository.Url, showValue: true),
        BranchName = buildContext.BuildServer.GetVariable("GitHubPagesRepositoryUrl", "gh-pages", showValue: true),
        Email = buildContext.BuildServer.GetVariable("GitHubPagesEmail", showValue: true),
        UserName = buildContext.BuildServer.GetVariable("GitHubPagesUserName", showValue: true),
        ApiToken = buildContext.BuildServer.GetVariable("GitHubPagesApiToken", showValue: false),
    };

    return data;
}

//-------------------------------------------------------------

List<string> _gitHubPages;

public List<string> GitHubPages
{
    get 
    {
        if (_gitHubPages is null)
        {
            _gitHubPages = new List<string>();
        }

        return _gitHubPages;
    }
}