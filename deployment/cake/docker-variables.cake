#l "buildserver.cake"

//-------------------------------------------------------------

public class DockerImagesContext : BuildContextWithItemsBase
{
    public DockerImagesContext(IBuildContext parentBuildContext)
        : base(parentBuildContext)
    {
    }

    public string DockerEngineUrl { get; set; }
    public string DockerRegistryUrl { get; set; }
    public string DockerRegistryUserName { get; set; }
    public string DockerRegistryPassword { get; set; }

    protected override void ValidateContext()
    {
    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' docker image projects");
    }
}

//-------------------------------------------------------------

private DockerImagesContext InitializeDockerImagesContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new DockerImagesContext(parentBuildContext)
    {
        Items = DockerImages ?? new List<string>(),
        DockerEngineUrl = buildContext.BuildServer.GetVariable("DockerEngineUrl", showValue: true),
        DockerRegistryUrl = buildContext.BuildServer.GetVariable("DockerRegistryUrl", showValue: true),
        DockerRegistryUserName = buildContext.BuildServer.GetVariable("DockerRegistryUserName", showValue: false),
        DockerRegistryPassword = buildContext.BuildServer.GetVariable("DockerRegistryPassword", showValue: false)
    };

    return data;
}

//-------------------------------------------------------------

List<string> _dockerImages;

public List<string> DockerImages
{
    get
    {
        if (_dockerImages is null)
        {
            _dockerImages = new List<string>();
        }

        return _dockerImages;
    }
}