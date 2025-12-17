#l "aspire-variables.cake"

using System.Xml.Linq;

//-------------------------------------------------------------

public class AspireProcessor : ProcessorBase
{
    public AspireProcessor(BuildContext buildContext)
        : base(buildContext)
    {

    }

    public override bool HasItems()
    {
        return BuildContext.Aspire.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Nothing needed
    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Nothing needed
    }

    public override async Task BuildAsync()
    {
        if (!HasItems())
        {
            return;
        }

        // Nothing needed 
    }

    public override async Task PackageAsync()
    {
        if (!HasItems())
        {
            return;
        }

        var aspireContext = BuildContext.Aspire;

        if (aspireContext.Items.Count > 1)
        {
            throw new InvalidOperationException("Multiple Aspire projects found. Please ensure only one Aspire project is defined in the solution.");
        }

        var environmentName = GetEnvironmentName(aspireContext);

        foreach (var aspireProject in aspireContext.Items)
        {
            if (BuildContext.General.SkipComponentsThatAreNotDeployable &&
                !ShouldPackageProject(BuildContext, aspireProject))
            {
                CakeContext.Information("Aspire project '{0}' should not be packaged", aspireProject);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Packaging Aspire project '{0}'", aspireProject);

            BuildContext.CakeContext.Information("Setting environment variables");

            var environmentVariables = new Dictionary<string, string>
            {
                { "AZURE_PRINCIPAL_ID", aspireContext.AzurePrincipalId },
                { "AZURE_PRINCIPAL_TYPE", aspireContext.AzurePrincipalType },
                { "AZURE_LOCATION", aspireContext.AzureLocation },
                { "AZURE_RESOURCE_GROUP", $"rg-{aspireContext.AzureResourceGroup}-{aspireContext.EnvironmentName}" },
                { "AZURE_SUBSCRIPTION_ID", aspireContext.AzureSubscriptionId },
                { "AZURE_ENV_NAME", aspireContext.EnvironmentName },
            };

            foreach (var environmentVariable in environmentVariables)
            {
                RunAzd($"env set {environmentVariable.Key}=\"{environmentVariable.Value}\" -e {environmentName} --no-prompt");
            }

            BuildContext.CakeContext.Information("Generating infrastructure context");

            RunAzd($"infra gen -e {environmentName} --force");

            BuildContext.CakeContext.LogSeparator();
        }
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }

        var aspireContext = BuildContext.Aspire;

        if (aspireContext.Items.Count > 1)
        {
            throw new InvalidOperationException("Multiple Aspire projects found. Please ensure only one Aspire project is defined in the solution.");
        }

        var environmentName = GetEnvironmentName(aspireContext);

        foreach (var aspireProject in aspireContext.Items)
        {
            if (!ShouldDeployProject(BuildContext, aspireProject))
            {
                CakeContext.Information("Aspire project '{0}' should not be deployed", aspireProject);
                continue;
            }

            BuildContext.CakeContext.LogSeparator("Deploying Aspire project '{0}'", aspireProject);

            try
            {
                BuildContext.CakeContext.Information("Logging in to Azure");

                RunAzd($"auth login --tenant-id {aspireContext.AzureTenantId} --client-id {aspireContext.AzureClientId} --client-secret {aspireContext.AzureClientSecret} --no-prompt");

                // Note: got weird errors when running provision and deploy manually, so using up instead

                BuildContext.CakeContext.Information("Deploying to Azure");

                RunAzd($"up -e {environmentName} --no-prompt");

                //BuildContext.CakeContext.Information("Provisioning infrastructure for Aspire project '{0}'", aspireProject);

                //RunAzd($"provision -e {environmentName}");

                //BuildContext.CakeContext.Information("Deploying Aspire project '{0}'", aspireProject);

                // Note: this could technically be improved in the future by using
                // azd deploy 'componentname'

                //RunAzd($"deploy --all -e {environmentName}");

                await BuildContext.Notifications.NotifyAsync(aspireProject, string.Format("Deployed to Azure"), TargetType.AspireProject);
            }
            finally
            {
                BuildContext.CakeContext.Information("Logging out of Azure");
            
                RunAzd($"auth logout");
            }

            BuildContext.CakeContext.LogSeparator();
        }
    }

    public override async Task FinalizeAsync()
    {
        // Nothing needed
    }

    private string GetEnvironmentName(AspireContext aspireContext)
    {
        // Because resource group names are set: "rg-{environmentName}" by Aspire, we automatically add
        // an extra name to the environment

        var environmentName = $"{aspireContext.AzureResourceGroup}-{aspireContext.EnvironmentName}";

        return environmentName;
    }

    private void RunAzd(string arguments)
    {
        if (BuildContext.CakeContext.StartProcess("azd", new ProcessSettings
        {
            Arguments = arguments
        }) != 0)
        {
            throw new CakeException("Azd failed failed. Please check the logs for more details.");
        }
    }
}