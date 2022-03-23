public class ContinuaCIBuildServer : BuildServerBase
{
    public ContinuaCIBuildServer(ICakeContext cakeContext)
        : base(cakeContext)
    {
    }

    //-------------------------------------------------------------
    
    public override async Task OnTestFailedAsync()
    {
        await ImportNUnitTestFilesAsync();     
    }

    //-------------------------------------------------------------

    public override async Task AfterTestAsync()
    {
        await ImportNUnitTestFilesAsync();     
    }

    //-------------------------------------------------------------

    private async Task ImportNUnitTestFilesAsync()
    {
        var continuaCIContext = GetContinuaCIContext();
        if (!continuaCIContext.IsRunningOnContinuaCI)
        {
            return;
        }

        var testResultsDirectory = System.IO.Path.Combine(BuildContext.General.OutputRootDirectory, "testresults");

        if (!CakeContext.DirectoryExists(testResultsDirectory))
        {            
            CakeContext.Warning("No test results directory");
            return;
        }

        var cakeFilePattern =  System.IO.Path.Combine(testResultsDirectory, "**", "*.xml");

        var testResultsFiles = CakeContext.GetFiles(cakeFilePattern);
        if (!testResultsFiles.Any())
        {            
            CakeContext.Warning($"No test result file found using '{cakeFilePattern}'");
            return;
        }

        var continuaCiFilePattern = System.IO.Path.Combine(testResultsDirectory, "**.xml");

        CakeContext.Information($"Importing NUnit test results from using '{continuaCiFilePattern}'");

        var message = $"@@continua[importUnitTestResults type='nunit' filePatterns='{continuaCiFilePattern}']";
        WriteIntegration(message);
    }

    //-------------------------------------------------------------

    public override async Task PinBuildAsync(string comment)
    {
        var continuaCIContext = GetContinuaCIContext();
        if (!continuaCIContext.IsRunningOnContinuaCI)
        {
            return;
        }

        CakeContext.Information("Pinning build in Continua CI");

        var message = string.Format("@@continua[pinBuild comment='{0}' appendComment='{1}']", 
            comment, !string.IsNullOrWhiteSpace(comment));
        WriteIntegration(message);
    }

    //-------------------------------------------------------------

    public override async Task SetVersionAsync(string version)
    {
        var continuaCIContext = GetContinuaCIContext();
        if (!continuaCIContext.IsRunningOnContinuaCI)
        {
            return;
        }

        CakeContext.Information("Setting version '{0}' in Continua CI", version);

        var message = string.Format("@@continua[setBuildVersion value='{0}']", version);
        WriteIntegration(message);
    }

    //-------------------------------------------------------------

    public override async Task SetVariableAsync(string variableName, string value)
    {
        var continuaCIContext = GetContinuaCIContext();
        if (!continuaCIContext.IsRunningOnContinuaCI)
        {
            return;
        }

        CakeContext.Information("Setting variable '{0}' to '{1}' in Continua CI", variableName, value);
    
        var message = string.Format("@@continua[setVariable name='{0}' value='{1}' skipIfNotDefined='true']", variableName, value);
        WriteIntegration(message);
    }

    //-------------------------------------------------------------

    public override Tuple<bool, string> GetVariable(string variableName, string defaultValue)
    {
        var continuaCIContext = GetContinuaCIContext();
        if (!continuaCIContext.IsRunningOnContinuaCI)
        {
            return new Tuple<bool, string>(false, string.Empty);
        }

        var exists = false;
        var value = string.Empty;

        var buildServerVariables = continuaCIContext.Environment.Variable;
        if (buildServerVariables.ContainsKey(variableName))
        {
            CakeContext.Information("Variable '{0}' is specified via Continua CI", variableName);
        
            exists = true;
            value = buildServerVariables[variableName];
        }
        
        return new Tuple<bool, string>(exists, value);
    }

    //-------------------------------------------------------------

    private IContinuaCIProvider GetContinuaCIContext()
    {
        return CakeContext.ContinuaCI();
    }

    //-------------------------------------------------------------

    private void WriteIntegration(string message)
    {
        // Must be Console.WriteLine
        CakeContext.Information(message);
    }
}