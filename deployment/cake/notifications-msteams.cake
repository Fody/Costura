#addin "nuget:?package=Cake.MicrosoftTeams&version=2.0.0"

//-------------------------------------------------------------

public class MsTeamsNotifier : INotifier
{
    public MsTeamsNotifier(BuildContext buildContext)
    {
        BuildContext = buildContext;

        WebhookUrl = buildContext.BuildServer.GetVariable("MsTeamsWebhookUrl", showValue: false);
        WebhookUrlForErrors = buildContext.BuildServer.GetVariable("MsTeamsWebhookUrlForErrors", WebhookUrl, showValue: false);
    }

    public BuildContext BuildContext { get; private set; }

    public string WebhookUrl { get; private set; }
    public string WebhookUrlForErrors { get; private set; }

    public string GetMsTeamsWebhookUrl(string project, TargetType targetType)
    {
        // Allow per target overrides via "MsTeamsWebhookUrlFor[TargetType]"
        var targetTypeUrl = GetTargetSpecificConfigurationValue(BuildContext, targetType, "MsTeamsWebhookUrlFor", string.Empty);
        if (!string.IsNullOrEmpty(targetTypeUrl))
        {
            return targetTypeUrl;
        }

        // Allow per project overrides via "MsTeamsWebhookUrlFor[ProjectName]"
        var projectTypeUrl = GetProjectSpecificConfigurationValue(BuildContext, project, "MsTeamsWebhookUrlFor", string.Empty);
        if (!string.IsNullOrEmpty(projectTypeUrl))
        {
            return projectTypeUrl;
        }

        // Return default fallback
        return WebhookUrl;
    }

    //-------------------------------------------------------------

    private string GetMsTeamsTarget(string project, TargetType targetType, NotificationType notificationType)
    {
        if (notificationType == NotificationType.Error)
        {
            return WebhookUrlForErrors;
        }

        return GetMsTeamsWebhookUrl(project, targetType);
    }

    //-------------------------------------------------------------

    public async Task NotifyAsync(string project, string message, TargetType targetType, NotificationType notificationType)
    {
        var targetWebhookUrl = GetMsTeamsTarget(project, targetType, notificationType);
        if (string.IsNullOrWhiteSpace(targetWebhookUrl))
        {
            return;
        }

        var messageCard = new MicrosoftTeamsMessageCard 
        {
            title = project,
            summary = notificationType.ToString(),
            sections = new []
            {
                new MicrosoftTeamsMessageSection
                {
                    activityTitle = notificationType.ToString(),
                    activitySubtitle = message,
                    activityText = " ",
                    activityImage = "https://raw.githubusercontent.com/cake-build/graphics/master/png/cake-small.png",
                    facts = new [] 
                    {
                        new MicrosoftTeamsMessageFacts { name ="Project", value = project },
                        new MicrosoftTeamsMessageFacts { name ="Version", value = BuildContext.General.Version.FullSemVer },
                        new MicrosoftTeamsMessageFacts { name ="CakeVersion", value = BuildContext.CakeContext.Environment.Runtime.CakeVersion.ToString() },
                        //new MicrosoftTeamsMessageFacts { name ="TargetFramework", value = Context.Environment.Runtime .TargetFramework.ToString() },
                    },
                }
            }
        };

        var result = BuildContext.CakeContext.MicrosoftTeamsPostMessage(messageCard, new MicrosoftTeamsSettings 
        {
            IncomingWebhookUrl = targetWebhookUrl
        });

        if (result != System.Net.HttpStatusCode.OK)
        {
            BuildContext.CakeContext.Warning(string.Format("MsTeams result: {0}", result));
        }
    }
}
