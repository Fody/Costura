// Customize this file when using a different issue tracker
#l "issuetrackers-github.cake"
#l "issuetrackers-jira.cake"

//-------------------------------------------------------------

public interface IIssueTracker
{
    Task CreateAndReleaseVersionAsync();
}

//-------------------------------------------------------------

public class IssueTrackerIntegration : IntegrationBase
{
    private readonly List<IIssueTracker> _issueTrackers = new List<IIssueTracker>();

    public IssueTrackerIntegration(BuildContext buildContext)
        : base(buildContext)
    {
        _issueTrackers.Add(new GitHubIssueTracker(buildContext));
        _issueTrackers.Add(new JiraIssueTracker(buildContext));
    }

    public async Task CreateAndReleaseVersionAsync()
    {
        BuildContext.CakeContext.LogSeparator("Creating and releasing version");

        foreach (var issueTracker in _issueTrackers)
        {
            try
            {
                await issueTracker.CreateAndReleaseVersionAsync();
            }
            catch (Exception ex)
            {
                BuildContext.CakeContext.Warning(ex.Message);
            }
        }
    }
}