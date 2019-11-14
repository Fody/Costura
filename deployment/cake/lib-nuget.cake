public class NuGetServer
{
    public string Url { get;set; }

    public string ApiKey { get;set; }

    public override string ToString()
    {
        var result = Url;

        result += string.Format(" (ApiKey present: '{0}')", !string.IsNullOrWhiteSpace(ApiKey));

        return result;
    }
}

//-------------------------------------------------------------

public static List<NuGetServer> GetNuGetServers(string urls, string apiKeys)
{
    var splittedUrls = urls.Split(new [] { ";" }, StringSplitOptions.None);
    var splittedApiKeys = apiKeys.Split(new [] { ";" }, StringSplitOptions.None);

    if (splittedUrls.Length != splittedApiKeys.Length)
    {
        throw new Exception("Number of api keys does not match number of urls. Even if an API key is not required, add an empty one");
    }

    var servers = new List<NuGetServer>();

    for (int i = 0; i < splittedUrls.Length; i++)
    {
        var url = splittedUrls[i];
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new Exception("Url for NuGet server cannot be empty");
        }

        servers.Add(new NuGetServer
        {
            Url = url,
            ApiKey = splittedApiKeys[i]
        });
    }

    return servers;
}