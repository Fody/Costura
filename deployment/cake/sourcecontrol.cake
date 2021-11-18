// Customize this file when using a different source controls
#l "sourcecontrol-github.cake"

//-------------------------------------------------------------

public interface ISourceControl
{
    Task MarkBuildAsPendingAsync(string context, string description);
    Task MarkBuildAsFailedAsync(string context, string description);
    Task MarkBuildAsSucceededAsync(string context, string description);
}

//-------------------------------------------------------------

public class SourceControlIntegration : IntegrationBase
{
    private readonly List<ISourceControl> _sourceControls = new List<ISourceControl>();

    public SourceControlIntegration(BuildContext buildContext)
        : base(buildContext)
    {
        _sourceControls.Add(new GitHubSourceControl(buildContext));
    }

    public async Task MarkBuildAsPendingAsync(string context, string description = null)
    {
        BuildContext.CakeContext.LogSeparator($"Marking build as pending: '{description ?? string.Empty}'");

        context = context ?? "default";
        description = description ?? "Build pending";

        foreach (var sourceControl in _sourceControls)
        {
            try
            {
                await sourceControl.MarkBuildAsPendingAsync(context, description);
            }
            catch (Exception ex)
            {
                BuildContext.CakeContext.Warning($"Failed to update status: {ex.Message}");
            }
        }
    }
    
    public async Task MarkBuildAsFailedAsync(string context, string description = null)
    {
        BuildContext.CakeContext.LogSeparator($"Marking build as failed: '{description ?? string.Empty}'");

        context = context ?? "default";
        description = description ?? "Build failed";

        foreach (var sourceControl in _sourceControls)
        {
            try
            {
                await sourceControl.MarkBuildAsFailedAsync(context, description);
            }
            catch (Exception ex)
            {
                BuildContext.CakeContext.Warning($"Failed to update status: {ex.Message}");
            }
        }
    }
    
    public async Task MarkBuildAsSucceededAsync(string context, string description = null)
    {
        BuildContext.CakeContext.LogSeparator($"Marking build as succeeded: '{description ?? string.Empty}'");

        context = context ?? "default";
        description = description ?? "Build succeeded";

        foreach (var sourceControl in _sourceControls)
        {
            try
            {
                await sourceControl.MarkBuildAsSucceededAsync(context, description);
            }
            catch (Exception ex)
            {
                BuildContext.CakeContext.Warning($"Failed to update status: {ex.Message}");
            }
        }
    }
}