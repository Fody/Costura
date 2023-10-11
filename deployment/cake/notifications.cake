#l "notifications-msteams.cake"
//#l "notifications-slack.cake"

//-------------------------------------------------------------

public enum NotificationType
{
    Info,

    Error
}

//-------------------------------------------------------------

public interface INotifier
{
    Task NotifyAsync(string project, string message, TargetType targetType = TargetType.Unknown, NotificationType notificationType = NotificationType.Info);
}

//-------------------------------------------------------------

public class NotificationsIntegration : IntegrationBase
{
    private readonly List<INotifier> _notifiers = new List<INotifier>();

    public NotificationsIntegration(BuildContext buildContext)
        : base(buildContext)
    {
        _notifiers.Add(new MsTeamsNotifier(buildContext));
    }

    public async Task NotifyDefaultAsync(string project, string message, TargetType targetType = TargetType.Unknown)
    {
        await NotifyAsync(project, message, targetType, NotificationType.Info);
    }

    //-------------------------------------------------------------

    public async Task NotifyErrorAsync(string project, string message, TargetType targetType = TargetType.Unknown)
    {
        await NotifyAsync(project, string.Format("ERROR: {0}", message), targetType, NotificationType.Error);
    }

    //-------------------------------------------------------------

    public async Task NotifyAsync(string project, string message, TargetType targetType = TargetType.Unknown, NotificationType notificationType = NotificationType.Info)
    {
        foreach (var notifier in _notifiers)
        {
            await notifier.NotifyAsync(project, message, targetType, notificationType);
        }
    }
}