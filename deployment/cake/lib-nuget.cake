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

//-------------------------------------------------------------

private static void RestoreNuGetPackages(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName)
{
    buildContext.CakeContext.Information("Restoring packages for {0}", solutionOrProjectFileName);
    
    var sources = SplitSeparatedList(buildContext.General.NuGet.PackageSources, ';');
    var runtimeIdentifiers = new List<string>(new [] 
    {
        "",
        "win-x64",
        "browser-wasm"
    });

    RestoreNuGetPackagesUsingNuGet(buildContext, solutionOrProjectFileName, sources);
    RestoreNuGetPackagesUsingDotnetRestore(buildContext, solutionOrProjectFileName, sources, runtimeIdentifiers);
}

//-------------------------------------------------------------

private static void RestoreNuGetPackagesUsingNuGet(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName, List<string> sources)
{
    buildContext.CakeContext.Information("Restoring packages for {0} using 'NuGet'", solutionOrProjectFileName);
    
    try
    {
        var nuGetRestoreSettings = new NuGetRestoreSettings
        {
            DisableParallelProcessing = false,
            NoCache = false,
        };
 
        if (sources.Count > 0)
        {
            nuGetRestoreSettings.Source = sources;
        }

        buildContext.CakeContext.NuGetRestore(solutionOrProjectFileName, nuGetRestoreSettings);
    }
    catch (Exception)
    {
        // Ignore
    }
}

//-------------------------------------------------------------

private static void RestoreNuGetPackagesUsingDotnetRestore(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName, List<string> sources, List<string> runtimeIdentifiers)
{
    buildContext.CakeContext.Information("Restoring packages for {0} using 'dotnet restore'", solutionOrProjectFileName);
        
    foreach (var runtimeIdentifier in runtimeIdentifiers)
    {
        try
        {
            var restoreSettings = new DotNetCoreRestoreSettings
            {
                IgnoreFailedSources = true,
                NoCache = false,
            };
    
            if (sources.Count > 0)
            {
                restoreSettings.Sources = sources;
            }

            if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                restoreSettings.Runtime = runtimeIdentifier;
            }

            buildContext.CakeContext.DotNetCoreRestore(solutionOrProjectFileName.FullPath, restoreSettings);
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}