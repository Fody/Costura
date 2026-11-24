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

    public string AzureFederatedCredentialProvider { get; set; }

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
            throw new InvalidOperationException("Azure principal ID is not set. Please set the 'AspireAzurePrincipalId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureLocation))
        {
            throw new InvalidOperationException("Azure location is not set. Please set the 'AspireAzureLocation' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureResourceGroup))
        {
            throw new InvalidOperationException("Azure resource group is not set. Please set the 'AspireAzureResourceGroup' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureSubscriptionId))
        {
            throw new InvalidOperationException("Azure subscription ID is not set. Please set the 'AspireAzureSubscriptionId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureTenantId))
        {
            throw new InvalidOperationException("Azure tenant ID is not set. Please set the 'AspireAzureTenantId' variable.");
        }

        if (string.IsNullOrWhiteSpace(AzureClientId))
        {
            throw new InvalidOperationException("Azure client ID is not set. Please set the 'AspireAzureClientId' variable.");
        }

        var hasClientSecret = !string.IsNullOrWhiteSpace(AzureClientSecret);
        var hasFederatedCredentialProvider = !string.IsNullOrWhiteSpace(AzureFederatedCredentialProvider);

        if (!hasClientSecret && !hasFederatedCredentialProvider)
        {
            throw new InvalidOperationException("Azure credentials are not set. Please set either 'AspireAzureClientSecret' or 'AspireAzureFederatedCredentialProvider'.");
        }

        if (hasClientSecret && hasFederatedCredentialProvider)
        {
            throw new InvalidOperationException("Please set only one of 'AspireAzureClientSecret' and 'AspireAzureFederatedCredentialProvider'.");
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
        AzureClientSecret = buildContext.BuildServer.GetVariable("AspireAzureClientSecret", showValue: false),
        AzureFederatedCredentialProvider = buildContext.BuildServer.GetVariable("AspireAzureFederatedCredentialProvider", showValue: true)
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