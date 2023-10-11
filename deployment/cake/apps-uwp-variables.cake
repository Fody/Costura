#l "./buildserver.cake"

//-------------------------------------------------------------

public class UwpContext : BuildContextWithItemsBase
{
    public UwpContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string WindowsStoreAppId { get; set; }
    public string WindowsStoreClientId { get; set; }
    public string WindowsStoreClientSecret { get; set; }
    public string WindowsStoreTenantId { get; set; }

    protected override void ValidateContext()
    {
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' uwp projects");
    }
}

//-------------------------------------------------------------

private UwpContext InitializeUwpContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new UwpContext(parentBuildContext)
    {
        Items = UwpApps ?? new List<string>(),
        WindowsStoreAppId = buildContext.BuildServer.GetVariable("WindowsStoreAppId", showValue: true),
        WindowsStoreClientId = buildContext.BuildServer.GetVariable("WindowsStoreClientId", showValue: false),
        WindowsStoreClientSecret = buildContext.BuildServer.GetVariable("WindowsStoreClientSecret", showValue: false),
        WindowsStoreTenantId = buildContext.BuildServer.GetVariable("WindowsStoreTenantId", showValue: false)
    };

    return data;
}

//-------------------------------------------------------------

List<string> _uwpApps;

public List<string> UwpApps
{
    get 
    {
        if (_uwpApps is null)
        {
            _uwpApps = new List<string>();
        }

        return _uwpApps;
    }
}