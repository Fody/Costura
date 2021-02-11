public class ContinuaCIBuildServer : IBuildServer
{
    public ContinuaCIBuildServer(ICakeContext cakeContext)
    {
        CakeContext = cakeContext;
    }

    public ICakeContext CakeContext { get; private set; }

    public void PinBuild(string comment)
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

    public void SetVersion(string version)
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

    public void SetVariable(string variableName, string value)
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

    public Tuple<bool, string> GetVariable(string variableName, string defaultValue)
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

    private IContinuaCIProvider GetContinuaCIContext()
    {
        return CakeContext.ContinuaCI();
    }

    private void WriteIntegration(string message)
    {
        // Must be Console.WriteLine
        CakeContext.Information(message);
    }
}