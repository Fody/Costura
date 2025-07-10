#l "buildserver.cake"

//-------------------------------------------------------------

public class AspireContext : BuildContextWithItemsBase
{
    public AspireContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string EnvironmentName { get; set; }

    public string AzurePrincipalId { get; set; }

    public string AzurePrincipalType { get; set; }

    public string AzureLocation { get; set; }

    public string AzureResourceGroup { get; set; }

    public string AzureSubscriptionId { get; set; }

    public string AzureTenantId { get; set; }

    public string AzureClientId { get; set; }

    public string AzureClientSecret { get; set; }

    protected override void ValidateContext()
    {
        if (Items.Count == 0)
        {
            return;
        }

        if (Items.Count > 1)
        {
            throw new InvalidOperationException("Multiple Aspire projects found. Please ensure only one Aspire project is defined in the solution.");
        }

        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            throw new InvalidOperationException("Environment name is not set. Please set the 'AspireEnvironment' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzurePrincipalId))
        {
            throw new InvalidOperationException("Azure principal ID is not set. Please set the 'AzurePrincipalId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureLocation))
        {
            throw new InvalidOperationException("Azure location is not set. Please set the 'AzureLocation' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureResourceGroup))
        {
            throw new InvalidOperationException("Azure resource group is not set. Please set the 'AzureResourceGroup' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureSubscriptionId))
        {
            throw new InvalidOperationException("Azure subscription ID is not set. Please set the 'AzureSubscriptionId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureTenantId))
        {
            throw new InvalidOperationException("Azure tenant ID is not set. Please set the 'AzureTenantId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureClientId))
        {
            throw new InvalidOperationException("Azure client ID is not set. Please set the 'AzureClientId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureClientSecret))
        {
            throw new InvalidOperationException("Azure client secret is not set. Please set the 'AzureClientSecret' variable.");
        }
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' Aspire projects");
    }
}

//-------------------------------------------------------------

private AspireContext InitializeAspireContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new AspireContext(parentBuildContext)
    {
        Items = AspireProjects ?? new List<string>(),
        EnvironmentName = buildContext.BuildServer.GetVariable("AspireEnvironment", "prod", showValue: true),
        AzurePrincipalId = buildContext.BuildServer.GetVariable("AspireAzurePrincipalId", showValue: true),
        AzurePrincipalType = buildContext.BuildServer.GetVariable("AspireAzurePrincipalType", "ManagedIdentity", showValue: true),
        AzureLocation = buildContext.BuildServer.GetVariable("AspireAzureLocation", showValue: true),
        AzureResourceGroup = buildContext.BuildServer.GetVariable("AspireAzureResourceGroup", showValue: true),
        AzureSubscriptionId = buildContext.BuildServer.GetVariable("AspireAzureSubscriptionId", showValue: true),
        AzureTenantId = buildContext.BuildServer.GetVariable("AspireAzureTenantId", showValue: true),
        AzureClientId = buildContext.BuildServer.GetVariable("AspireAzureClientId", showValue: true),
        AzureClientSecret = buildContext.BuildServer.GetVariable("AspireAzureClientSecret", showValue: false)
    };

    return data;
}

//-------------------------------------------------------------

List<string> _aspireProjects;

public List<string> AspireProjects
{
    get 
    {
        if (_aspireProjects is null)
        {
            _aspireProjects = new List<string>();
        }

        return _aspireProjects;
    }
}