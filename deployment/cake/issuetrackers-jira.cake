#tool "nuget:?package=JiraCli&version=1.3.0-alpha0338&prerelease"

//-------------------------------------------------------------

public class JiraIssueTracker : IIssueTracker
{
    public JiraIssueTracker(BuildContext buildContext)
    {
        BuildContext = buildContext;

        Url = buildContext.BuildServer.GetVariable("JiraUrl", showValue: true);
        Username = buildContext.BuildServer.GetVariable("JiraUsername", showValue: true);
        Password = buildContext.BuildServer.GetVariable("JiraPassword", showValue: false);
        ProjectName = buildContext.BuildServer.GetVariable("JiraProjectName", showValue: true);

        if (!string.IsNullOrWhiteSpace(Url) &&
            !string.IsNullOrWhiteSpace(ProjectName))
        {
            IsAvailable = true;
        }
    }

    public BuildContext BuildContext { get; private set; }

    public string Url { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string ProjectName { get; set; }
    public bool IsAvailable { get; private set; }

    public async Task CreateAndReleaseVersionAsync()
    {
        if (!IsAvailable)
        {
            BuildContext.CakeContext.Information("JIRA is not available, skipping JIRA integration");
            return;
        }

        var version = BuildContext.General.Version.FullSemVer;

        BuildContext.CakeContext.Information("Releasing version '{0}' in JIRA", version);

        // Example call:
        // JiraCli.exe -url %JiraUrl% -user %JiraUsername% -pw %JiraPassword% -action createandreleaseversion 
        // -project %JiraProjectName% -version %GitVersion_FullSemVer% -merge %IsOfficialBuild%

        var nugetPath = BuildContext.CakeContext.Tools.Resolve("JiraCli.exe");
        BuildContext.CakeContext.StartProcess(nugetPath, new ProcessSettings 
        {
            Arguments = new ProcessArgumentBuilder()
                .AppendSwitch("-url", Url)
                .AppendSwitch("-user", Username)
                .AppendSwitchSecret("-pw", Password)
                .AppendSwitch("-action", "createandreleaseversion")
                .AppendSwitch("-project", ProjectName)
                .AppendSwitch("-version", version)
                .AppendSwitch("-merge", BuildContext.General.IsOfficialBuild.ToString())
        });

        BuildContext.CakeContext.Information("Released version in JIRA");
    }
}