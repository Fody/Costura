// Customize this file when using a different issue tracker
// #l "buildserver-github.cake"
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
        _issueTrackers.Add(new JiraIssueTracker(buildContext));
    }

    public async Task CreateAndReleaseVersionAsync()
    {
        BuildContext.CakeContext.LogSeparator("Creating and releasing version");

        foreach (var issueTracker in _issueTrackers)
        {
            await issueTracker.CreateAndReleaseVersionAsync();
        }
    }
}