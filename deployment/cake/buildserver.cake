// Customize this file when using a different build server
#l "buildserver-continuaci.cake"

using System.Runtime.InteropServices;

public interface IBuildServer
{
    void PinBuild(string comment);
    void SetVersion(string version);
    void SetVariable(string name, string value);
    Tuple<bool, string> GetVariable(string variableName, string defaultValue);
}

//-------------------------------------------------------------

public class BuildServerIntegration : IIntegration
{
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    static extern uint GetPrivateProfileString(
        string lpAppName, 
        string lpKeyName,
        string lpDefault, 
        StringBuilder lpReturnedString, 
        uint nSize,
        string lpFileName);

    private readonly Dictionary<string, object> _parameters;
    private readonly List<IBuildServer> _buildServers = new List<IBuildServer>();
    private readonly Dictionary<string, string> _buildServerVariableCache = new Dictionary<string, string>();

    // This is a special integration that only gets ICakeContext, not the BuildContext
    public BuildServerIntegration(ICakeContext cakeContext, Dictionary<string, object> parameters)
    {
        CakeContext = cakeContext;
        _parameters = parameters;

        _buildServers.Add(new ContinuaCIBuildServer(cakeContext));
    }

    public ICakeContext CakeContext { get; private set; }

    public void PinBuild(string comment)
    {
        foreach (var buildServer in _buildServers)
        {
            buildServer.PinBuild(comment);
        }        
    }

    //-------------------------------------------------------------

    public void SetVersion(string version)
    {
        foreach (var buildServer in _buildServers)
        {
            buildServer.SetVersion(version);
        }
    }

    //-------------------------------------------------------------

    public void SetVariable(string variableName, string value)
    {
        foreach (var buildServer in _buildServers)
        {
            buildServer.SetVariable(variableName, value);
        }
    }

    //-------------------------------------------------------------

    public bool GetVariableAsBool(string variableName, bool defaultValue, bool showValue = false)
    {
        var value = defaultValue;

        if (bool.TryParse(GetVariable(variableName, "unknown", showValue: false), out var retrievedValue))
        {
            value = retrievedValue;
        }

        if (showValue)
        {
            PrintVariableValue(variableName, value.ToString());
        }

        return value;
    }

    //-------------------------------------------------------------

    public string GetVariable(string variableName, string defaultValue = null, bool showValue = false)
    {
        var cacheKey = string.Format("{0}__{1}", variableName ?? string.Empty, defaultValue ?? string.Empty);

        if (!_buildServerVariableCache.TryGetValue(cacheKey, out string value))
        {
            value = GetVariableForCache(variableName, defaultValue);

            if (showValue)
            {
                PrintVariableValue(variableName, value);
            }

            _buildServerVariableCache[cacheKey] = value;
        }
        //else
        //{
        //    Information("Retrieved value for '{0}' from cache", variableName);
        //}
        
        return value;
    }

    //-------------------------------------------------------------

    private string GetVariableForCache(string variableName, string defaultValue = null)
    {
        var argumentValue = CakeContext.Argument(variableName, "non-existing");
        if (argumentValue != "non-existing")
        {
            CakeContext.Information("Variable '{0}' is specified via an argument", variableName);

            return argumentValue;
        }

        // Check each build server
        foreach (var buildServer in _buildServers)
        {
            var buildServerVariable = buildServer.GetVariable(variableName, defaultValue);
            if (buildServerVariable.Item1)
            {
                return buildServerVariable.Item2;
            }
        }

        var overrideFile = System.IO.Path.Combine(".", "build.cakeoverrides");
        if (System.IO.File.Exists(overrideFile))
        {
            var sb = new StringBuilder(string.Empty, 256);
            var lengthRead = GetPrivateProfileString("General", variableName, null, sb, (uint)sb.Capacity, overrideFile);
            if (lengthRead > 0)
            {
                CakeContext.Information("Variable '{0}' is specified via build.cakeoverrides", variableName);
            
                var sbValue = sb.ToString();
                if (sbValue == "[ignore]" ||
                    sbValue == "[empty]")
                {
                    return string.Empty;
                }

                return sbValue;
            }
        }
        
        if (CakeContext.HasEnvironmentVariable(variableName))
        {
            CakeContext.Information("Variable '{0}' is specified via an environment variable", variableName);
        
            return CakeContext.EnvironmentVariable(variableName);
        }
        
        if (_parameters.TryGetValue(variableName, out var parameter))
        {
            CakeContext.Information("Variable '{0}' is specified via the Parameters dictionary", variableName);
        
            if (parameter is null)
            {
                return null;
            }
        
            if (parameter is string)
            {
                return (string)parameter;
            }
            
            if (parameter is Func<string>)
            {
                return ((Func<string>)parameter).Invoke();
            }
            
            throw new Exception(string.Format("Parameter is defined as '{0}', but that type is not supported yet...", parameter.GetType().Name));
        }
        
        CakeContext.Information("Variable '{0}' is not specified, returning default value", variableName);
        
        return defaultValue ?? string.Empty;
    }
    
    //-------------------------------------------------------------

    private void PrintVariableValue(string variableName, string value, bool isSensitive = false)
    {
        var valueForLog = isSensitive ? "********" : value;
        CakeContext.Information("{0}: '{1}'", variableName, valueForLog);
    }
}

