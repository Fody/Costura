#addin "nuget:?package=Cake.GitHub&version=0.1.0"
#addin "nuget:?package=Octokit&version=9.1.1"

//-------------------------------------------------------------

public class GitHubSourceControl : ISourceControl
{
    public GitHubSourceControl(BuildContext buildContext)
    {
        BuildContext = buildContext;

        ApiKey = buildContext.BuildServer.GetVariable("GitHubApiKey", buildContext.General.Repository.Password, showValue: false);
        OwnerName = buildContext.BuildServer.GetVariable("GitHubOwnerName", buildContext.General.Copyright.Company, showValue: true);
        ProjectName = buildContext.BuildServer.GetVariable("GitHubProjectName", buildContext.General.Solution.Name, showValue: true);

        if (!string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(OwnerName) &&
            !string.IsNullOrWhiteSpace(ProjectName))
        {
            IsAvailable = true;
        }
    }

    public BuildContext BuildContext { get; private set; }

    public string ApiKey { get; set; }
    public string OwnerName { get; set; }
    public string ProjectName { get; set; }

    public string OwnerAndProjectName 
    {
        get { return $"{OwnerName}/{ProjectName}"; }
    }

    public bool IsAvailable { get; private set; }

    public async Task MarkBuildAsPendingAsync(string context, string description)
    {
        UpdateStatus(GitHubStatusState.Pending, context, description);
    }
    
    public async Task MarkBuildAsFailedAsync(string context, string description)
    {
        UpdateStatus(GitHubStatusState.Failure, context, description);
    }
    
    public async Task MarkBuildAsSucceededAsync(string context, string description)
    {
        UpdateStatus(GitHubStatusState.Success, context, description);
    }

    private void UpdateStatus(GitHubStatusState state, string context, string description)
    {
        // Disabled for now
        return;

        if (!IsAvailable)
        {
            return;
        }

        BuildContext.CakeContext.Information("Updating GitHub status to '{0}' | '{1}'", state, description);

        var commitSha = BuildContext.General.Repository.CommitId;

        // Note: UserName is not really required, use string.Empty, then only api key is needed
        BuildContext.CakeContext.GitHubStatus(string.Empty, ApiKey, OwnerName, ProjectName, commitSha, new GitHubStatusSettings
        {
            State = state,
            TargetUrl = null,// "url-to-build-server",
            Description = description,
            Context = $"Cake - {context}"
        });
    }
}