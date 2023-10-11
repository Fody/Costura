#tool "nuget:?package=gitreleasemanager&version=0.15.0"

//-------------------------------------------------------------

public class GitHubIssueTracker : IIssueTracker
{
    public GitHubIssueTracker(BuildContext buildContext)
    {
        BuildContext = buildContext;

        UserName = buildContext.BuildServer.GetVariable("GitHubUserName", showValue: true);
        ApiKey = buildContext.BuildServer.GetVariable("GitHubApiKey", showValue: false);
        OwnerName = buildContext.BuildServer.GetVariable("GitHubOwnerName", buildContext.General.Copyright.Company, showValue: true);
        ProjectName = buildContext.BuildServer.GetVariable("GitHubProjectName", buildContext.General.Solution.Name, showValue: true);

        if (!string.IsNullOrWhiteSpace(UserName) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(OwnerName) &&
            !string.IsNullOrWhiteSpace(ProjectName))
        {
            IsAvailable = true;
        }
    }

    public BuildContext BuildContext { get; private set; }

    public string UserName { get; set; }
    public string ApiKey { get; set; }
    public string OwnerName { get; set; }
    public string ProjectName { get; set; }

    public string OwnerAndProjectName 
    {
        get { return $"{OwnerName}/{ProjectName}"; }
    }

    public bool IsAvailable { get; private set; }

    public async Task CreateAndReleaseVersionAsync()
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("GitHub is not available, skipping GitHub integration");
            return;
        }

        var version = BuildContext.General.Version.FullSemVer;

        BuildContext.CakeContext.Information("Releasing version '{0}' in GitHub", version);

        // For docs, see https://cakebuild.net/dsl/gitreleasemanager/

        BuildContext.CakeContext.Information("Step 1 / 4: Creating release");

        BuildContext.CakeContext.GitReleaseManagerCreate(ApiKey, OwnerName, ProjectName, new GitReleaseManagerCreateSettings
        {
            TargetDirectory = BuildContext.General.RootDirectory,
            Milestone = BuildContext.General.Version.MajorMinorPatch,
            Name = version,
            Prerelease = !BuildContext.General.IsOfficialBuild,
            TargetCommitish = BuildContext.General.Repository.CommitId
        });

        BuildContext.CakeContext.Information("Step 2 / 4: Adding assets to the release (not supported yet)");

        // Not yet supported

        if (!BuildContext.General.IsOfficialBuild)
        {
            BuildContext.CakeContext.Information("GitHub release publishing only runs against non-prerelease builds");
        }
        else
        {
            BuildContext.CakeContext.Information("Step 3 / 4: Publishing release");

            BuildContext.CakeContext.GitReleaseManagerPublish(ApiKey, OwnerName, ProjectName, BuildContext.General.Version.MajorMinorPatch, new GitReleaseManagerPublishSettings
            {
                TargetDirectory = BuildContext.General.RootDirectory
            });

            BuildContext.CakeContext.Information("Step 4 / 4: Closing the milestone");

            BuildContext.CakeContext.GitReleaseManagerClose(ApiKey, OwnerName, ProjectName, BuildContext.General.Version.MajorMinorPatch, new GitReleaseManagerCloseMilestoneSettings
            {
                TargetDirectory = BuildContext.General.RootDirectory
            });
        }

        BuildContext.CakeContext.Information("Released version in GitHub");
    }
}