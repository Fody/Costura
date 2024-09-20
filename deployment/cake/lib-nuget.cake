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

    var runtimeIdentifiers = new [] 
    {
        "win-x86",
        "win-x64",
        "win-arm64",
        "browser-wasm"
    };

    var supportedRuntimeIdentifiers = GetProjectRuntimesIdentifiers(buildContext, solutionOrProjectFileName, runtimeIdentifiers);

    RestoreNuGetPackagesUsingNuGet(buildContext, solutionOrProjectFileName, sources, supportedRuntimeIdentifiers);
    RestoreNuGetPackagesUsingDotnetRestore(buildContext, solutionOrProjectFileName, sources, supportedRuntimeIdentifiers);
}

//-------------------------------------------------------------

private static void RestoreNuGetPackagesUsingNuGet(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName, IReadOnlyList<string> sources, IReadOnlyList<string> runtimeIdentifiers)
{
    if (!buildContext.General.NuGet.RestoreUsingNuGet)
    {
        return;
    }

    buildContext.CakeContext.LogSeparator("Restoring packages for '{0}' using 'NuGet'", solutionOrProjectFileName);
    
    // No need to deal with runtime identifiers

    try
    {
        var nuGetRestoreSettings = new NuGetRestoreSettings
        {
            DisableParallelProcessing = false,
            NoCache = false,
            NonInteractive = true,
            RequireConsent = false
        };

        if (sources.Count > 0)
        {
            nuGetRestoreSettings.Source = sources.ToList();
        }

        buildContext.CakeContext.NuGetRestore(solutionOrProjectFileName, nuGetRestoreSettings);
    }
    catch (Exception)
    {
        // Ignore
    }
}

//-------------------------------------------------------------

private static void RestoreNuGetPackagesUsingDotnetRestore(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName, IReadOnlyList<string> sources, IReadOnlyList<string> runtimeIdentifiers)
{
    if (!buildContext.General.NuGet.RestoreUsingDotNetRestore)
    {
        return;
    }

    buildContext.CakeContext.LogSeparator("Restoring packages for '{0}' using 'dotnet restore'", solutionOrProjectFileName);
 
    foreach (var runtimeIdentifier in runtimeIdentifiers)
    {
        try
        {
            buildContext.CakeContext.LogSeparator("Restoring packages for '{0}' using 'dotnet restore' using runtime identifier '{1}'", solutionOrProjectFileName, runtimeIdentifier);

            var restoreSettings = new DotNetRestoreSettings
            {
                DisableParallel = false,
                Force = false,
                ForceEvaluate = false,
                IgnoreFailedSources = true,
                NoCache = false,
                NoDependencies = buildContext.General.NuGet.NoDependencies, // use true to speed up things
                Verbosity = DotNetVerbosity.Normal
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
                restoreSettings.Sources = sources.ToList();
            }

            using (buildContext.CakeContext.UseDiagnosticVerbosity())
            {
                buildContext.CakeContext.DotNetRestore(solutionOrProjectFileName.FullPath, restoreSettings);
            }
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}