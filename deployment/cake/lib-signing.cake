#tool "dotnet:?package=AzureSignTool&version=6.0.0"
#tool "dotnet:?package=NuGetKeyVaultSignTool&version=3.2.3"

private static string _signToolFileName;
private static string _azureSignToolFileName;

//-------------------------------------------------------------

public static bool ShouldSignImmediately(BuildContext buildContext, string projectName)
{
	// Sometimes unit tests require signed assemblies, but only sign immediately when it's in the list
    if (buildContext.CodeSigning.ProjectsToSignImmediately.Contains(projectName))
    {   
        buildContext.CakeContext.Information($"Immediately code signing '{projectName}' files");
        return true;
    }

    if (buildContext.General.IsLocalBuild ||
        buildContext.General.IsCiBuild)
    {
        // Never code-sign local or ci builds
        return false;
    }

    return false;
}

//-------------------------------------------------------------

public static void SignProjectFiles(BuildContext buildContext, string projectName)
{
    var outputDirectory = string.Format("{0}/{1}", buildContext.General.OutputRootDirectory, projectName);

    var codeSignContext = buildContext.General.CodeSign;
    var codeSignWildCard = codeSignContext.WildCard;
    if (string.IsNullOrWhiteSpace(codeSignWildCard))
    {
        // Empty, we need to override with project name for valid default value
        codeSignWildCard = projectName;
    }

    SignFilesInDirectory(buildContext, outputDirectory, codeSignWildCard);
}

//-------------------------------------------------------------

public static void SignFilesInDirectory(BuildContext buildContext, string directory, string codeSignWildCard)
{
    var codeSignContext = buildContext.General.CodeSign;
    var azureCodeSignContext = buildContext.General.AzureCodeSign;

    if (buildContext.General.IsLocalBuild ||
        buildContext.General.IsCiBuild)
    {
        // Never code-sign local or ci builds
        return;
    }

    if (!codeSignContext.IsAvailable &&
        !azureCodeSignContext.IsAvailable)
    {
        buildContext.CakeContext.Information("Skipping code signing because none of the options is available");
        return;
    }

    var projectFilesToSign = new List<FilePath>();

    if (!string.IsNullOrWhiteSpace(codeSignWildCard))
    {
        // Make sure the pattern becomes *[wildcard]*
        codeSignWildCard += "*";
    }
    else
    {
        codeSignWildCard = string.Empty;
    }

    var exeSignFilesSearchPattern = string.Format("{0}/**/*{1}.exe", directory, codeSignWildCard);
    buildContext.CakeContext.Information(exeSignFilesSearchPattern);
    projectFilesToSign.AddRange(buildContext.CakeContext.GetFiles(exeSignFilesSearchPattern));

    var dllSignFilesSearchPattern = string.Format("{0}/**/*{1}.dll", directory, codeSignWildCard);
    buildContext.CakeContext.Information(dllSignFilesSearchPattern);
    projectFilesToSign.AddRange(buildContext.CakeContext.GetFiles(dllSignFilesSearchPattern));

    buildContext.CakeContext.Information("Found '{0}' files to code sign", projectFilesToSign.Count);

    var signToolCommand = GetSignToolCommandLine(buildContext);

    SignFiles(buildContext, signToolCommand, projectFilesToSign, null);
}

//-------------------------------------------------------------

public static void SignFile(BuildContext buildContext, FilePath filePath)
{
    SignFile(buildContext, filePath.FullPath);
}

//-------------------------------------------------------------

public static void SignFile(BuildContext buildContext, string fileName)
{
    var signToolCommand = GetSignToolCommandLine(buildContext);

    SignFiles(buildContext, signToolCommand, new [] { fileName }, null);
}

//-------------------------------------------------------------

public static void SignFiles(BuildContext buildContext, string signToolCommand, IEnumerable<FilePath> fileNames, string additionalCommandLineArguments)
{
    if (fileNames.Any())
    {
        buildContext.CakeContext.Information($"Signing '{fileNames.Count()}' files, this could take a while...");
    }

    foreach (var fileName in fileNames)
    {
        SignFile(buildContext, signToolCommand, fileName.FullPath, additionalCommandLineArguments);
    }
}

//-------------------------------------------------------------

public static void SignFiles(BuildContext buildContext, string signToolCommand, IEnumerable<string> fileNames, string additionalCommandLineArguments)
{    
    if (fileNames.Any())
    {
        buildContext.CakeContext.Information($"Signing '{fileNames.Count()}' files, this could take a while...");
    }
    
    foreach (var fileName in fileNames)
    {
        SignFile(buildContext, signToolCommand, fileName, additionalCommandLineArguments);
    }
}

//-------------------------------------------------------------

public static void SignFile(BuildContext buildContext, string signToolCommand, string fileName, string additionalCommandLineArguments)
{
    var codeSignContext = buildContext.General.CodeSign;
    var azureCodeSignContext = buildContext.General.AzureCodeSign;

    if (string.IsNullOrWhiteSpace(_signToolFileName))
    {
        // Always fetch, it is used for verification
        _signToolFileName = FindWindowsSignToolFileName(buildContext);   
    }

    if (string.IsNullOrWhiteSpace(_azureSignToolFileName))
    {
        _azureSignToolFileName = FindAzureSignToolFileName(buildContext);
    }

    var signToolFileName = _signToolFileName;
    
    // Azure always wins
    if (azureCodeSignContext.IsAvailable)
    {
        signToolFileName = _azureSignToolFileName;
    }

    SignFile(buildContext, signToolFileName, signToolCommand, fileName, additionalCommandLineArguments);
}

//-------------------------------------------------------------

public static void SignFile(BuildContext buildContext, string signToolFileName, string signToolCommand, string fileName, string additionalCommandLineArguments)
{
    // Skip code signing in specific scenarios
    if (string.IsNullOrWhiteSpace(signToolCommand))
    {
        return;
    }

    if (string.IsNullOrWhiteSpace(signToolFileName))
    {
        throw new InvalidOperationException("Cannot find signtool, make sure to install a Windows Development Kit");
    }

    buildContext.CakeContext.Information(string.Empty);

    // Retry mechanism, signing with timestamping is not as reliable as we thought
    var safetyCounter = 3;

    while (safetyCounter > 0)
    {
        buildContext.CakeContext.Information($"Ensuring file '{fileName}' is signed...");

        // Check
        var checkProcessSettings = new ProcessSettings
        {
            Arguments = $"verify /pa \"{fileName}\"",
            Silent = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        // Note: we can safely use SignTool.exe here

        using (var checkProcess = buildContext.CakeContext.StartAndReturnProcess(_signToolFileName, checkProcessSettings))
        {
            checkProcess.WaitForExit();

            var exitCode = checkProcess.GetExitCode();
            if (exitCode == 0)
            {
                buildContext.CakeContext.Information($"File '{fileName}' is already signed, skipping...");
                buildContext.CakeContext.Information(string.Empty);
                return;
            }
        }

        // Sign
        if (!string.IsNullOrWhiteSpace(additionalCommandLineArguments))
        {
            signToolCommand += $" {additionalCommandLineArguments}";
        }

        var finalCommand = $"{signToolCommand} \"{fileName}\"";

        buildContext.CakeContext.Information($"File '{fileName}' is not signed, signing using '{finalCommand}'");

        var signProcessSettings = new ProcessSettings
        {
            Arguments = finalCommand,
            Silent = true
        };

        using (var signProcess = buildContext.CakeContext.StartAndReturnProcess(signToolFileName, signProcessSettings))
        {
            signProcess.WaitForExit();

            var exitCode = signProcess.GetExitCode();
            if (exitCode == 0)
            {
                return;
            }

            buildContext.CakeContext.Warning($"Failed to sign '{fileName}', retries left: '{safetyCounter}'");

            // Important: add a delay!
            System.Threading.Thread.Sleep(5 * 1000);
        }

        safetyCounter--;
    }

    // If we get here, we failed
    throw new Exception($"Signing of '{fileName}' failed");
}

//-------------------------------------------------------------

public static void SignNuGetPackage(BuildContext buildContext, string fileName)
{
    var codeSignContext = buildContext.General.CodeSign;
    var azureCodeSignContext = buildContext.General.AzureCodeSign;

    if (buildContext.General.IsCiBuild || 
        buildContext.General.IsLocalBuild)
    {
        return;
    }
    
    if (!codeSignContext.IsAvailable &&
        !azureCodeSignContext.IsAvailable)
    {
        buildContext.CakeContext.Information("Skipping code signing because none of the options is available");
        return;
    }

    buildContext.CakeContext.Information($"Signing NuGet package '{fileName}'");

    if (azureCodeSignContext.IsAvailable)
    {
        var signToolFileName = FindNuGetAzureSignToolFileName(buildContext);
        var signToolCommandLine = string.Format("sign -kvu {0} -kvt {1} -kvi {2} -kvs {3} -kvc {4} -tr {5} -fd {6}", 
            azureCodeSignContext.VaultUrl,
            azureCodeSignContext.TenantId,
            azureCodeSignContext.ClientId,
            azureCodeSignContext.ClientSecret,
            azureCodeSignContext.CertificateName,
            azureCodeSignContext.TimeStampUri,
            azureCodeSignContext.HashAlgorithm);

        var finalCommand = $"{signToolFileName} {signToolCommandLine} {fileName}";

        buildContext.CakeContext.Information($"{finalCommand}'");

        SignFile(buildContext, signToolFileName, signToolCommandLine, fileName, null);

        return;
    }

    if  (codeSignContext.IsAvailable)
    {
        var exitCode = buildContext.CakeContext.StartProcess(buildContext.General.NuGet.Executable, new ProcessSettings
        {
            Arguments = $"sign \"{fileName}\" -CertificateSubjectName \"{codeSignContext.CertificateSubjectName}\" -Timestamper \"{codeSignContext.TimeStampUri}\""
        });

        buildContext.CakeContext.Information("Signing NuGet package exited with '{0}'", exitCode);

        return;
    }
    
    throw new NotSupportedException("No supported code signing method could be found");
}

//-------------------------------------------------------------

public static string FindWindowsSignToolFileName(BuildContext buildContext)
{
    var directory = FindLatestWindowsKitsDirectory(buildContext);
    if (directory != null)
    {
        return System.IO.Path.Combine(directory, "x64", "signtool.exe");
    }

    return null;
}

//-------------------------------------------------------------

public static string FindAzureSignToolFileName(BuildContext buildContext)
{
    var path = buildContext.CakeContext.Tools.Resolve("AzureSignTool.exe");

    buildContext.CakeContext.Information("Found path '{0}'", path);

    return path.FullPath;
}

//-------------------------------------------------------------

public static string FindNuGetAzureSignToolFileName(BuildContext buildContext)
{
    var path = buildContext.CakeContext.Tools.Resolve("NuGetKeyVaultSignTool.exe");

    buildContext.CakeContext.Information("Found path '{0}'", path);

    return path.FullPath;
}

//-------------------------------------------------------------

public static string GetSignToolFileName(BuildContext buildContext)
{
    var codeSignContext = buildContext.General.CodeSign;
    var azureCodeSignContext = buildContext.General.AzureCodeSign;

    // Azure first
    if (azureCodeSignContext.IsAvailable)
    {
        return FindAzureSignToolFileName(buildContext);
    }

    if (codeSignContext.IsAvailable)
    {
        return FindWindowsSignToolFileName(buildContext);
    }

    return string.Empty;
}

//-------------------------------------------------------------

public static string GetSignToolCommandLine(BuildContext buildContext)
{
    var codeSignContext = buildContext.General.CodeSign;
    var azureCodeSignContext = buildContext.General.AzureCodeSign;

    var signToolCommand = string.Empty;

    if (codeSignContext.IsAvailable)
    {
        signToolCommand = string.Format("sign /a /t {0} /n {1} /fd {2}", 
            codeSignContext.TimeStampUri, 
            codeSignContext.CertificateSubjectName, 
            codeSignContext.HashAlgorithm);
    }

    // Note: Azure always wins
    if (azureCodeSignContext.IsAvailable)
    {
        signToolCommand = string.Format("sign -kvu {0} -kvt {1} -kvi {2} -kvs {3} -kvc {4} -tr {5} -fd {6}", 
            azureCodeSignContext.VaultUrl,
            azureCodeSignContext.TenantId,
            azureCodeSignContext.ClientId,
            azureCodeSignContext.ClientSecret,
            azureCodeSignContext.CertificateName,
            azureCodeSignContext.TimeStampUri,
            azureCodeSignContext.HashAlgorithm);
    }

    return signToolCommand;
}
