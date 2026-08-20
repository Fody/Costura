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

private static void InitializeNuGetPackageSources(BuildContext buildContext)
{
    buildContext.CakeContext.LogSeparator("Configuring NuGet package sources");
    
    var sources = SplitSeparatedListKeepEmptyEntries(buildContext.General.NuGet.PackageSources, ';');

    var sourcesNames = SplitSeparatedListKeepEmptyEntries(buildContext.General.NuGet.PackageSourcesNames, ';');
    if (sourcesNames.Count > 0)
    {
        if (sourcesNames.Count != sources.Count)
        {
            throw new Exception("Number of package source names does not match number of package sources. Even if a token is not required, add an empty one");
        }
    }

    var sourcesUsernames = SplitSeparatedListKeepEmptyEntries(buildContext.General.NuGet.PackageSourcesUsernames, ';');
    if (sourcesUsernames.Count > 0)
    {
        if (sourcesUsernames.Count != sources.Count)
        {
            throw new Exception("Number of package source usernames does not match number of package sources. Even if a token is not required, add an empty one");
        }
    }

    var sourcesTokens = SplitSeparatedListKeepEmptyEntries(buildContext.General.NuGet.PackageSourcesTokens, ';');
    if (sourcesTokens.Count > 0)
    {
        if (sourcesTokens.Count != sources.Count)
        {
            throw new Exception("Number of package source tokens does not match number of package sources. Even if a token is not required, add an empty one");
        }
    }

    for (var i = 0; i < sources.Count; i++)
    {
        var source = sources[i];
        var name = sourcesNames.Count > 0 ? sourcesNames[i] : source;
        var username = sourcesUsernames.Count > 0 ? sourcesUsernames[i] : null;
        var token = sourcesTokens.Count > 0 ? sourcesTokens[i] : null;

        var settings = new NuGetSourcesSettings {
            UserName = username,
            Password = token,
            StorePasswordInClearText = true
        };

        var tokenForLogging = "not-available";
        if (!string.IsNullOrWhiteSpace(token))
        {
            tokenForLogging = $"**** (length: {token.Length})";

            if (token.Length > 10)
            {
                tokenForLogging = token.Substring(0, 5) + $"... (length: {token.Length})";
            }
        }

        buildContext.CakeContext.Information("Registering NuGet feed '{0}'", source);
        buildContext.CakeContext.Information("* Name: {0}", name);
        buildContext.CakeContext.Information("* Username: {0}", username);
        buildContext.CakeContext.Information("* Token: {0}", tokenForLogging);

        if (!buildContext.CakeContext.NuGetHasSource(source))
        {
            buildContext.CakeContext.NuGetAddSource(name, source, settings);
        }

        buildContext.CakeContext.Information(string.Empty);
    }
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