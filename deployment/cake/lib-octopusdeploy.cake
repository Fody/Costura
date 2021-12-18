#tool "nuget:?package=OctopusTools&version=7.4.3550"

public class OctopusDeployIntegration : IntegrationBase
{
    public OctopusDeployIntegration(BuildContext buildContext)
        : base(buildContext)
    {
        OctopusRepositoryUrl = buildContext.BuildServer.GetVariable("OctopusRepositoryUrl", showValue: true);
        OctopusRepositoryApiKey = buildContext.BuildServer.GetVariable("OctopusRepositoryApiKey", showValue: false);
        OctopusDeploymentTarget = buildContext.BuildServer.GetVariable("OctopusDeploymentTarget", "Staging", showValue: true);
    }

    public string OctopusRepositoryUrl { get; set; }
    public string OctopusRepositoryApiKey { get; set; }
    public string OctopusDeploymentTarget { get; set; }

    //-------------------------------------------------------------

    public string GetRepositoryUrl(string projectName)
    {
        // Allow per project overrides via "OctopusRepositoryUrlFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "OctopusRepositoryUrlFor", OctopusRepositoryUrl);
    }

    //-------------------------------------------------------------

    public string GetRepositoryApiKey(string projectName)
    {
        // Allow per project overrides via "OctopusRepositoryApiKeyFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "OctopusRepositoryApiKeyFor", OctopusRepositoryApiKey);
    }

    //-------------------------------------------------------------

    public string GetDeploymentTarget(string projectName)
    {
        // Allow per project overrides via "OctopusDeploymentTargetFor[ProjectName]"
        return GetProjectSpecificConfigurationValue(BuildContext, projectName, "OctopusDeploymentTargetFor", OctopusDeploymentTarget);
    }
}