private static string _signToolFileName;

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
    var certificateSubjectName = buildContext.General.CodeSign.CertificateSubjectName;
    if (string.IsNullOrWhiteSpace(certificateSubjectName))
    {
        buildContext.CakeContext.Information("Skipping code signing because the certificate subject name was not specified");
        return;
    }

    var codeSignWildCard = buildContext.General.CodeSign.WildCard;
    if (string.IsNullOrWhiteSpace(codeSignWildCard))
    {
        // Empty, we need to override with project name for valid default value
        codeSignWildCard = projectName;
    }

    var outputDirectory = string.Format("{0}/{1}", buildContext.General.OutputRootDirectory, projectName);

    var projectFilesToSign = new List<FilePath>();

    var exeSignFilesSearchPattern = string.Format("{0}/**/*{1}*.exe", outputDirectory, codeSignWildCard);
    buildContext.CakeContext.Information(exeSignFilesSearchPattern);
    projectFilesToSign.AddRange(buildContext.CakeContext.GetFiles(exeSignFilesSearchPattern));

    var dllSignFilesSearchPattern = string.Format("{0}/**/*{1}*.dll", outputDirectory, codeSignWildCard);
    buildContext.CakeContext.Information(dllSignFilesSearchPattern);
    projectFilesToSign.AddRange(buildContext.CakeContext.GetFiles(dllSignFilesSearchPattern));

    buildContext.CakeContext.Information("Found '{0}' files to code sign for '{1}'", projectFilesToSign.Count, projectName);

    var signToolCommand = string.Format("sign /a /t {0} /n {1} /fd {2}", buildContext.General.CodeSign.TimeStampUri, 
        certificateSubjectName, buildContext.General.CodeSign.HashAlgorithm);

    SignFiles(buildContext, signToolCommand, projectFilesToSign);
}

//-------------------------------------------------------------

public static void SignFiles(BuildContext buildContext, string signToolCommand, IEnumerable<FilePath> fileNames, string additionalCommandLineArguments = null)
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

public static void SignFiles(BuildContext buildContext, string signToolCommand, IEnumerable<string> fileNames, string additionalCommandLineArguments = null)
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

public static void SignFile(BuildContext buildContext, string signToolCommand, string fileName, string additionalCommandLineArguments = null)
{
    // Skip code signing in specific scenarios
    if (string.IsNullOrWhiteSpace(signToolCommand))
    {
        return;
    }

    if (string.IsNullOrWhiteSpace(_signToolFileName))
    {
        _signToolFileName = FindSignToolFileName(buildContext);
    }

    if (string.IsNullOrWhiteSpace(_signToolFileName))
    {
        throw new InvalidOperationException("Cannot find signtool.exe, make sure to install a Windows Development Kit");
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

        using (var signProcess = buildContext.CakeContext.StartAndReturnProcess(_signToolFileName, signProcessSettings))
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

public static string FindSignToolFileName(BuildContext buildContext)
{
    var directory = FindLatestWindowsKitsDirectory(buildContext);
    if (directory != null)
    {
        return System.IO.Path.Combine(directory, "x64", "signtool.exe");
    }

    return null;
}
