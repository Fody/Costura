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
    buildContext.CakeContext.LogSeparator("Restoring packages for '{0}'", solutionOrProjectFileName);
    
    var sources = SplitSeparatedList(buildContext.General.NuGet.PackageSources, ';');
    var runtimeIdentifiers = new List<string>(new [] 
    {
        "win-x64",
        "browser-wasm"
    });

    RestoreNuGetPackagesUsingNuGet(buildContext, solutionOrProjectFileName, sources);
    RestoreNuGetPackagesUsingDotnetRestore(buildContext, solutionOrProjectFileName, sources, runtimeIdentifiers);
}

//-------------------------------------------------------------

private static void RestoreNuGetPackagesUsingNuGet(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName, List<string> sources)
{
    if (!buildContext.General.NuGet.RestoreUsingNuGet)
    {
        return;
    }

    buildContext.CakeContext.LogSeparator("Restoring packages for '{0}' using 'NuGet'", solutionOrProjectFileName);
    
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
    if (!buildContext.General.NuGet.RestoreUsingDotNetRestore)
    {
        return;
    }

    buildContext.CakeContext.LogSeparator("Restoring packages for '{0}' using 'dotnet restore'", solutionOrProjectFileName);
        
    var projectFileContents = System.IO.File.ReadAllText(solutionOrProjectFileName.FullPath)?.ToLower();

    var supportedRuntimeIdentifiers = new List<string>();

    foreach (var runtimeIdentifier in runtimeIdentifiers)
    {
        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            if (!projectFileContents.Contains(runtimeIdentifier.ToLower()))
            {
                buildContext.CakeContext.Information("Project '{0}' does not support runtime identifier '{1}', skipping restore for this runtime identifier", solutionOrProjectFileName, runtimeIdentifier);
                continue;
            }
        }

        supportedRuntimeIdentifiers.Add(runtimeIdentifier);
    }

    if (supportedRuntimeIdentifiers.Count == 0)
    {
        // Default
        supportedRuntimeIdentifiers.Add(string.Empty);
    }

    foreach (var runtimeIdentifier in supportedRuntimeIdentifiers)
    {
        try
        {
            buildContext.CakeContext.LogSeparator("Restoring packages for '{0}' using 'dotnet restore' using runtime identifier '{1}'", solutionOrProjectFileName, runtimeIdentifier);

            var restoreSettings = new DotNetCoreRestoreSettings
            {
                DisableParallel = false,
                Force = false,
                ForceEvaluate = false,
                IgnoreFailedSources = true,
                NoCache = false,
                NoDependencies = buildContext.General.NuGet.NoDependencies, // use true to speed up things
                Verbosity = DotNetCoreVerbosity.Normal
            };
    
            if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                buildContext.CakeContext.Information("Project restore uses explicit runtime identifier, forcing re-evaluation");

                restoreSettings.Force = true;
                restoreSettings.ForceEvaluate = true;
                restoreSettings.Runtime = runtimeIdentifier;
            }

            if (sources.Count > 0)
            {
                restoreSettings.Sources = sources;
            }

            using (buildContext.CakeContext.UseDiagnosticVerbosity())
            {
                buildContext.CakeContext.DotNetCoreRestore(solutionOrProjectFileName.FullPath, restoreSettings);
            }
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}