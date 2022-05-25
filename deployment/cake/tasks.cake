#pragma warning disable CS1998

#l "lib-generic.cake"
#l "lib-logging.cake"
#l "lib-msbuild.cake"
#l "lib-nuget.cake"
#l "lib-signing.cake"
#l "lib-sourcelink.cake"
#l "issuetrackers.cake"
#l "installers.cake"
#l "sourcecontrol.cake"
#l "notifications.cake"
#l "generic-tasks.cake"
#l "apps-uwp-tasks.cake"
#l "apps-web-tasks.cake"
#l "apps-wpf-tasks.cake"
#l "codesigning-tasks.cake"
#l "components-tasks.cake"
#l "dependencies-tasks.cake"
#l "tools-tasks.cake"
#l "docker-tasks.cake"
#l "github-pages-tasks.cake"
#l "vsextensions-tasks.cake"
#l "tests.cake"
#l "templates-tasks.cake"

#addin "nuget:?package=Cake.FileHelpers&version=5.0.0"
#addin "nuget:?package=Cake.Sonar&version=1.1.30"
#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Newtonsoft.Json&version=13.0.1"
#addin "nuget:?package=System.Net.Http&version=4.3.4"

// Note: the SonarQube tool must be installed as a global .NET tool:
// `dotnet tool install --global dotnet-sonarscanner --ignore-failed-sources`
//#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.8.0"
#tool "nuget:?package=dotnet-sonarscanner&version=5.5.3"

//-------------------------------------------------------------
// BACKWARDS COMPATIBILITY CODE - START
//-------------------------------------------------------------

// Required so we have backwards compatibility, so developers can keep using
// GetBuildServerVariable in build.cake
private BuildServerIntegration _buildServerIntegration = null;

private BuildServerIntegration GetBuildServerIntegration()
{
    if (_buildServerIntegration is null)
    {
        _buildServerIntegration = new BuildServerIntegration(Context, Parameters);
    }

    return _buildServerIntegration;
}

public string GetBuildServerVariable(string variableName, string defaultValue = null, bool showValue = false)
{
    var buildServerIntegration = GetBuildServerIntegration();
    return buildServerIntegration.GetVariable(variableName, defaultValue, showValue);
}

//-------------------------------------------------------------
// BACKWARDS COMPATIBILITY CODE - END
//-------------------------------------------------------------

//-------------------------------------------------------------
// BUILD CONTEXT
//-------------------------------------------------------------

public class BuildContext : BuildContextBase
{
    public BuildContext(ICakeContext cakeContext)
        : base(cakeContext)
    {
        Processors = new List<IProcessor>();
        AllProjects = new List<string>();
        Variables = new Dictionary<string, string>();  
    }

    public List<IProcessor> Processors { get; private set; }
    public Dictionary<string, object> Parameters { get; set; }
    public Dictionary<string, string> Variables { get; private set; }
    
    // Integrations
    public BuildServerIntegration BuildServer { get; set; }
    public IssueTrackerIntegration IssueTracker { get; set; }
    public InstallerIntegration Installer { get; set; }
    public NotificationsIntegration Notifications { get; set; }
    public SourceControlIntegration SourceControl { get; set; }
    public OctopusDeployIntegration OctopusDeploy { get; set; }

    // Contexts
    public GeneralContext General { get; set; }
    public TestsContext Tests { get; set; }

    public CodeSigningContext CodeSigning { get; set; }
    public ComponentsContext Components { get; set; }
    public DependenciesContext Dependencies { get; set; }
    public DockerImagesContext DockerImages { get; set; }
    public GitHubPagesContext GitHubPages { get; set; }
    public TemplatesContext Templates { get; set; }
    public ToolsContext Tools { get; set; }
    public UwpContext Uwp { get; set; }
    public VsExtensionsContext VsExtensions { get; set; }
    public WebContext Web { get; set; }
    public WpfContext Wpf { get; set; }

    public List<string> AllProjects { get; private set; }

    protected override void ValidateContext()
    {
    }
    
    protected override void LogStateInfoForContext()
    {
    }
}

//-------------------------------------------------------------
// TASKS
//-------------------------------------------------------------

Setup<BuildContext>(setupContext =>
{
    setupContext.Information("Running setup of build scripts");

    var buildContext = new BuildContext(setupContext);

    // Important, set parameters first
    buildContext.Parameters = Parameters ?? new Dictionary<string, object>();

    setupContext.LogSeparator("Creating integrations");

    //  Important: build server first so other integrations can read values from config
    buildContext.BuildServer = GetBuildServerIntegration();
    buildContext.BuildServer.SetBuildContext(buildContext);

    setupContext.LogSeparator("Creating build context");

    buildContext.General = InitializeGeneralContext(buildContext, buildContext);
    buildContext.Tests = InitializeTestsContext(buildContext, buildContext);

    buildContext.CodeSigning = InitializeCodeSigningContext(buildContext, buildContext);
    buildContext.Components = InitializeComponentsContext(buildContext, buildContext);
    buildContext.Dependencies = InitializeDependenciesContext(buildContext, buildContext);
    buildContext.DockerImages = InitializeDockerImagesContext(buildContext, buildContext);
    buildContext.GitHubPages = InitializeGitHubPagesContext(buildContext, buildContext);
    buildContext.Templates = InitializeTemplatesContext(buildContext, buildContext);
    buildContext.Tools = InitializeToolsContext(buildContext, buildContext);
    buildContext.Uwp = InitializeUwpContext(buildContext, buildContext);
    buildContext.VsExtensions = InitializeVsExtensionsContext(buildContext, buildContext);
    buildContext.Web = InitializeWebContext(buildContext, buildContext);
    buildContext.Wpf = InitializeWpfContext(buildContext, buildContext);

    // Other integrations last
    buildContext.IssueTracker = new IssueTrackerIntegration(buildContext);
    buildContext.Installer = new InstallerIntegration(buildContext);
    buildContext.Notifications = new NotificationsIntegration(buildContext);
    buildContext.OctopusDeploy = new OctopusDeployIntegration(buildContext);
    buildContext.SourceControl = new SourceControlIntegration(buildContext);

    setupContext.LogSeparator("Validating build context");

    buildContext.Validate();

    setupContext.LogSeparator("Creating processors");

    // Note: always put templates and dependencies processor first (it's a dependency after all)
    buildContext.Processors.Add(new TemplatesProcessor(buildContext));
    buildContext.Processors.Add(new DependenciesProcessor(buildContext));
    buildContext.Processors.Add(new ComponentsProcessor(buildContext));
    buildContext.Processors.Add(new DockerImagesProcessor(buildContext));
    buildContext.Processors.Add(new GitHubPagesProcessor(buildContext));
    buildContext.Processors.Add(new ToolsProcessor(buildContext));
    buildContext.Processors.Add(new UwpProcessor(buildContext));
    buildContext.Processors.Add(new VsExtensionsProcessor(buildContext));
    buildContext.Processors.Add(new WebProcessor(buildContext));
    buildContext.Processors.Add(new WpfProcessor(buildContext));
    // !!! Note: we add test projects *after* preparing all the other processors, see Prepare task !!!

    setupContext.LogSeparator("Registering variables for templates");

    // Preparing variables for templates
    buildContext.Variables["GitVersion_MajorMinorPatch"] = buildContext.General.Version.MajorMinorPatch;
    buildContext.Variables["GitVersion_FullSemVer"] = buildContext.General.Version.FullSemVer;
    buildContext.Variables["GitVersion_NuGetVersion"] = buildContext.General.Version.NuGet;

    setupContext.LogSeparator("Build context is ready, displaying state info");

    buildContext.LogStateInfo();

    return buildContext;
});

//-------------------------------------------------------------

Task("Initialize")
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforeInitializeAsync();

    buildContext.CakeContext.LogSeparator("Writing special values back to build server");

    var displayVersion = buildContext.General.Version.FullSemVer;
    if (buildContext.General.IsCiBuild)
    {
        displayVersion += " ci";
    }

    await buildContext.BuildServer.SetVersionAsync(displayVersion);

    var variablesToUpdate = new Dictionary<string, string>();
    variablesToUpdate["channel"] = buildContext.Wpf.Channel;
    variablesToUpdate["publishType"] = buildContext.General.Solution.PublishType.ToString();
    variablesToUpdate["isAlphaBuild"] = buildContext.General.IsAlphaBuild.ToString();
    variablesToUpdate["isBetaBuild"] = buildContext.General.IsBetaBuild.ToString();
    variablesToUpdate["isOfficialBuild"] = buildContext.General.IsOfficialBuild.ToString();

    // Also write back versioning (then it can be cached), "worst case scenario" it's writing back the same versions
    variablesToUpdate["GitVersion_MajorMinorPatch"] = buildContext.General.Version.MajorMinorPatch;
    variablesToUpdate["GitVersion_FullSemVer"] = buildContext.General.Version.FullSemVer;
    variablesToUpdate["GitVersion_NuGetVersion"] = buildContext.General.Version.NuGet;
    variablesToUpdate["GitVersion_CommitsSinceVersionSource"] = buildContext.General.Version.CommitsSinceVersionSource;

    foreach (var variableToUpdate in variablesToUpdate)
    {
        await buildContext.BuildServer.SetVariableAsync(variableToUpdate.Key, variableToUpdate.Value);
    }

    await buildContext.BuildServer.AfterInitializeAsync();
});

//-------------------------------------------------------------

Task("Prepare")
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforePrepareAsync();

    foreach (var processor in buildContext.Processors)
    {
        await processor.PrepareAsync();
    }

    // Now add all projects, but dependencies first & tests last
    buildContext.AllProjects.AddRange(buildContext.Dependencies.Items);
    buildContext.AllProjects.AddRange(buildContext.Components.Items);
    buildContext.AllProjects.AddRange(buildContext.DockerImages.Items);
    buildContext.AllProjects.AddRange(buildContext.GitHubPages.Items);
    buildContext.AllProjects.AddRange(buildContext.Tools.Items);
    buildContext.AllProjects.AddRange(buildContext.Uwp.Items);
    buildContext.AllProjects.AddRange(buildContext.VsExtensions.Items);
    buildContext.AllProjects.AddRange(buildContext.Web.Items);
    buildContext.AllProjects.AddRange(buildContext.Wpf.Items);

    // Once we know all the projects that will be built, we calculate which
    // test projects need to be built as well

    var testProcessor = new TestProcessor(buildContext);

    await testProcessor.PrepareAsync();

    buildContext.Processors.Add(testProcessor);

    buildContext.AllProjects.AddRange(buildContext.Tests.Items);

    buildContext.CakeContext.LogSeparator("Final projects to process");

    foreach (var item in buildContext.AllProjects.ToList())
    {
        buildContext.CakeContext.Information($"- {item}");
    }
    
    await buildContext.BuildServer.AfterPrepareAsync();
});

//-------------------------------------------------------------

Task("UpdateInfo")
    .IsDependentOn("Prepare")
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforeUpdateInfoAsync();

    UpdateSolutionAssemblyInfo(buildContext);
    
    foreach (var processor in buildContext.Processors)
    {
        await processor.UpdateInfoAsync();
    }

    await buildContext.BuildServer.AfterUpdateInfoAsync();
});

//-------------------------------------------------------------

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("RestorePackages")
    .IsDependentOn("UpdateInfo")
    //.IsDependentOn("VerifyDependencies")
    .IsDependentOn("CleanupCode")
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforeBuildAsync();

    await buildContext.SourceControl.MarkBuildAsPendingAsync("Build");
    
    var sonarUrl = buildContext.General.SonarQube.Url;

    var enableSonar = !buildContext.General.SonarQube.IsDisabled && 
                      buildContext.General.IsCiBuild && // Only build on CI (all projects need to be included)
                      !string.IsNullOrWhiteSpace(sonarUrl);
    if (enableSonar)
    {
        var sonarSettings = new SonarBeginSettings 
        {
            // SonarQube info
            Url = sonarUrl,

            // Project info
            Key = buildContext.General.SonarQube.Project,
            Version = buildContext.General.Version.FullSemVer,

            // Use core clr version of SonarQube
            UseCoreClr = true,

            // Minimize extreme logging
            Verbose = false,
            Silent = true,

            // Support waiting for the quality gate
            ArgumentCustomization = args => args
                .Append("/d:sonar.qualitygate.wait=true")
        };

        if (!string.IsNullOrWhiteSpace(buildContext.General.SonarQube.Organization))
        {
            sonarSettings.Organization = buildContext.General.SonarQube.Organization;
        }

        if (!string.IsNullOrWhiteSpace(buildContext.General.SonarQube.Username))
        {
            sonarSettings.Login = buildContext.General.SonarQube.Username;
        }

        if (!string.IsNullOrWhiteSpace(buildContext.General.SonarQube.Password))
        {
            sonarSettings.Password = buildContext.General.SonarQube.Password;
        }

        // see https://cakebuild.net/api/Cake.Sonar/SonarBeginSettings/ for more information on
        // what to set for SonarCloud

        // Branch only works with the branch plugin. Documentation A says it's outdated, but
        // B still mentions it:
        // A: https://docs.sonarqube.org/latest/branches/overview/
        // B: https://docs.sonarqube.org/latest/analysis/analysis-parameters/
        if (buildContext.General.SonarQube.SupportBranches)
        {
            // TODO: How to support PR?
            sonarSettings.Branch = buildContext.General.Repository.BranchName;
        }

        Information("Beginning SonarQube");

        SonarBegin(sonarSettings);
    }
    else
    {
        Information("Skipping Sonar integration since url is not specified or it has been explicitly disabled");
    }

    try
    {
        if (buildContext.General.Solution.BuildSolution)
        {
            BuildSolution(buildContext);
        }

        foreach (var processor in buildContext.Processors)
        {
            if (processor is TestProcessor)
            {
                // Build test projects *after* SonarQube (not part of SQ analysis)
                continue;
            }

            await processor.BuildAsync();
        }
    }
    finally
    {
        if (enableSonar)
        {
            try
            {
                await buildContext.SourceControl.MarkBuildAsPendingAsync("SonarQube");

                var sonarEndSettings = new SonarEndSettings
                {
                    // Use core clr version of SonarQube
                    UseCoreClr = true
                };

                if (!string.IsNullOrWhiteSpace(buildContext.General.SonarQube.Username))
                {
                    sonarEndSettings.Login = buildContext.General.SonarQube.Username;
                }

                if (!string.IsNullOrWhiteSpace(buildContext.General.SonarQube.Password))
                {
                    sonarEndSettings.Password = buildContext.General.SonarQube.Password;
                }

                Information("Ending SonarQube");

                SonarEnd(sonarEndSettings);

                await buildContext.SourceControl.MarkBuildAsSucceededAsync("SonarQube");
            }
            catch (Exception)
            {
                var projectSpecificSonarUrl = $"{sonarUrl}/dashboard?id={buildContext.General.SonarQube.Project}";

                if (buildContext.General.SonarQube.SupportBranches)
                {
                    projectSpecificSonarUrl += $"&branch={buildContext.General.Repository.BranchName}";
                }

                var failedDescription = $"SonarQube failed, please visit '{projectSpecificSonarUrl}' for more details";

                await buildContext.SourceControl.MarkBuildAsFailedAsync("SonarQube", failedDescription);

                throw;
            }
        }
    }

    var testProcessor = buildContext.Processors.FirstOrDefault(x => x is TestProcessor) as TestProcessor;
    if (testProcessor is not null)
    {
        // Build test projects *after* SonarQube (not part of SQ analysis). Unfortunately, because of this, we cannot yet mark
        // the build as succeeded once we end the SQ session. Therefore, if SQ fails, both the SQ *and* build checks
        // will be marked as failed if SQ fails.
        await testProcessor.BuildAsync();
    }

    await buildContext.SourceControl.MarkBuildAsSucceededAsync("Build");

    Information("Completed build for version '{0}'", buildContext.General.Version.NuGet);

    await buildContext.BuildServer.AfterBuildAsync(); 
})
.OnError<BuildContext>(async (ex, buildContext) => 
{
    await buildContext.SourceControl.MarkBuildAsFailedAsync("Build");

    await buildContext.BuildServer.OnBuildFailedAsync(); 

    throw ex;
});

//-------------------------------------------------------------

Task("Test")
    .IsDependentOn("Prepare")
    // Note: no dependency on 'build' since we might have already built the solution
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforeTestAsync(); 

    await buildContext.SourceControl.MarkBuildAsPendingAsync("Test");
    
    foreach (var testProject in buildContext.Tests.Items)
    {
        buildContext.CakeContext.LogSeparator("Running tests for '{0}'", testProject);

        RunUnitTests(buildContext, testProject);
    }

    await buildContext.SourceControl.MarkBuildAsSucceededAsync("Test");

    Information("Completed tests for version '{0}'", buildContext.General.Version.NuGet);

    await buildContext.BuildServer.AfterTestAsync(); 
})
.OnError<BuildContext>(async (ex, buildContext) => 
{
    await buildContext.SourceControl.MarkBuildAsFailedAsync("Test");

    await buildContext.BuildServer.OnTestFailedAsync(); 

    throw ex;
});

//-------------------------------------------------------------

Task("Package")
    // Make sure to update info so our SolutionAssemblyInfo.cs is up to date
    .IsDependentOn("UpdateInfo")
    // Note: no dependency on 'build' since we might have already built the solution
    // Make sure we have the temporary "project.assets.json" in case we need to package with Visual Studio
    .IsDependentOn("RestorePackages")
    // Make sure to update if we are running on a new agent so we can sign nuget packages
    .IsDependentOn("UpdateNuGet")
    .IsDependentOn("CodeSign")
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforePackageAsync(); 

    foreach (var processor in buildContext.Processors)
    {
        await processor.PackageAsync();
    }

    Information("Completed packaging for version '{0}'", buildContext.General.Version.NuGet);

    await buildContext.BuildServer.AfterPackageAsync(); 
});

//-------------------------------------------------------------

Task("PackageLocal")
    .IsDependentOn("Package")
    .Does<BuildContext>(buildContext =>
{
    // Note: no build server integration calls since this is *local*

    // For now only package components, we might need to move this to components-tasks.cake in the future
    if (buildContext.Components.Items.Count == 0 && 
        buildContext.Tools.Items.Count == 0)
    {
        return;
    }

    var localPackagesDirectory = buildContext.General.NuGet.LocalPackagesDirectory;

    Information("Copying build artifacts to '{0}'", localPackagesDirectory);
    
    CreateDirectory(localPackagesDirectory);

    foreach (var component in buildContext.Components.Items)
    {
        try
        {
            Information("Copying build artifact for '{0}'", component);
        
            var sourceFile = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, 
                $"{component}.{buildContext.General.Version.NuGet}.nupkg");
                
            CopyFiles(new [] { sourceFile }, localPackagesDirectory);
        }
        catch (Exception)
        {
            // Ignore
            Warning("Failed to copy build artifacts for '{0}'", component);
        }
    }

    Information("Copied build artifacts for version '{0}'", buildContext.General.Version.NuGet);
});

//-------------------------------------------------------------

Task("Deploy")
    // Note: no dependency on 'package' since we might have already packaged the solution
    // Make sure we have the temporary "project.assets.json" in case we need to package with Visual Studio
    .IsDependentOn("RestorePackages")
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforeDeployAsync(); 

    foreach (var processor in buildContext.Processors)
    {
        await processor.DeployAsync();
    }
    
    await buildContext.BuildServer.AfterDeployAsync(); 
});

//-------------------------------------------------------------

Task("Finalize")
    // Note: no dependency on 'deploy' since we might have already deployed the solution
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.BuildServer.BeforeFinalizeAsync(); 

    Information("Finalizing release '{0}'", buildContext.General.Version.FullSemVer);

    foreach (var processor in buildContext.Processors)
    {
        await processor.FinalizeAsync();
    }

    if (buildContext.General.IsOfficialBuild)
    {
        await buildContext.BuildServer.PinBuildAsync("Official build");
    }

    await buildContext.IssueTracker.CreateAndReleaseVersionAsync();

    await buildContext.BuildServer.AfterFinalizeAsync(); 
});

//-------------------------------------------------------------
// Wrapper tasks since we don't want to add "Build" as a 
// dependency to "Package" because we want to run in multiple
// stages
//-------------------------------------------------------------

Task("BuildAndTest")
    .IsDependentOn("Initialize")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

//-------------------------------------------------------------

Task("BuildAndPackage")
    .IsDependentOn("Initialize")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Package");

//-------------------------------------------------------------

Task("BuildAndPackageLocal")
    .IsDependentOn("Initialize")
    .IsDependentOn("Build")
    //.IsDependentOn("Test") // Note: don't test for performance on local builds
    .IsDependentOn("PackageLocal");

//-------------------------------------------------------------

Task("BuildAndDeploy")
    .IsDependentOn("Initialize")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Package")
    .IsDependentOn("Deploy");

//-------------------------------------------------------------

Task("Default")    
    .Does<BuildContext>(async buildContext =>
{
    Error("No target specified, please specify one of the following targets:\n" +
          " - Prepare\n" +
          " - UpdateInfo\n" +
          " - Build\n" + 
          " - Test\n" + 
          " - Package\n" + 
          " - Deploy\n" + 
          " - Finalize\n\n" + 
          "or one of the combined ones:\n" +
          " - BuildAndTest\n" + 
          " - BuildAndPackage\n" + 
          " - BuildAndPackageLocal\n" + 
          " - BuildAndDeploy\n");
});

//-------------------------------------------------------------
// Test wrappers
//-------------------------------------------------------------

Task("TestNotifications")    
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.Notifications.NotifyAsync("MyProject", "This is a generic test");
    await buildContext.Notifications.NotifyAsync("MyProject", "This is a component test", TargetType.Component);
    await buildContext.Notifications.NotifyAsync("MyProject", "This is a docker image test", TargetType.DockerImage);
    await buildContext.Notifications.NotifyAsync("MyProject", "This is a web app test", TargetType.WebApp);
    await buildContext.Notifications.NotifyAsync("MyProject", "This is a wpf app test", TargetType.WpfApp);
    await buildContext.Notifications.NotifyErrorAsync("MyProject", "This is an error");
});

//-------------------------------------------------------------

Task("TestSourceControl")    
    .Does<BuildContext>(async buildContext =>
{
    await buildContext.SourceControl.MarkBuildAsPendingAsync("Build");

    await System.Threading.Tasks.Task.Delay(5 * 1000);

    await buildContext.SourceControl.MarkBuildAsSucceededAsync("Build");

    await buildContext.SourceControl.MarkBuildAsPendingAsync("Test");

    await System.Threading.Tasks.Task.Delay(5 * 1000);

    await buildContext.SourceControl.MarkBuildAsSucceededAsync("Test");
});

//-------------------------------------------------------------
// ACTUAL RUNNER - MUST BE DEFINED AT THE BOTTOM
//-------------------------------------------------------------

var localTarget = GetBuildServerVariable("Target", "Default", showValue: true);
RunTarget(localTarget);
