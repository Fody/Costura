// Customize this file when using a different build server
#l "buildserver-continuaci.cake"

using System.Runtime.InteropServices;

public interface IBuildServer
{
    Task PinBuildAsync(string comment);
    Task SetVersionAsync(string version);
    Task SetVariableAsync(string name, string value);
    Tuple<bool, string> GetVariable(string variableName, string defaultValue);

    void SetBuildContext(BuildContext buildContext);

    Task BeforeInitializeAsync();
    Task AfterInitializeAsync();

    Task BeforePrepareAsync();
    Task AfterPrepareAsync();

    Task BeforeUpdateInfoAsync();
    Task AfterUpdateInfoAsync();

    Task BeforeBuildAsync();
    Task OnBuildFailedAsync();
    Task AfterBuildAsync();

    Task BeforeTestAsync();
    Task OnTestFailedAsync();
    Task AfterTestAsync();

    Task BeforePackageAsync();
    Task AfterPackageAsync();

    Task BeforeDeployAsync();
    Task AfterDeployAsync();

    Task BeforeFinalizeAsync();
    Task AfterFinalizeAsync();
}

public abstract class BuildServerBase : IBuildServer
{
    protected BuildServerBase(ICakeContext cakeContext)
    {
        CakeContext = cakeContext;
    }

    public ICakeContext CakeContext { get; private set; }

    public BuildContext BuildContext { get; private set; }

    public abstract Task PinBuildAsync(string comment);
    public abstract Task SetVersionAsync(string version);
    public abstract Task SetVariableAsync(string name, string value);
    public abstract Tuple<bool, string> GetVariable(string variableName, string defaultValue);

    //-------------------------------------------------------------

    public void SetBuildContext(BuildContext buildContext)
    {
        BuildContext = buildContext;
    }

    //-------------------------------------------------------------

    public virtual async Task BeforeInitializeAsync()
    {
    }

    public virtual async Task AfterInitializeAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforePrepareAsync()
    {
    }

    public virtual async Task AfterPrepareAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforeUpdateInfoAsync()
    {
    }

    public virtual async Task AfterUpdateInfoAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforeBuildAsync()
    {
    }

    public virtual async Task OnBuildFailedAsync()
    {
    }

    public virtual async Task AfterBuildAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforeTestAsync()
    {
    }

    public virtual async Task OnTestFailedAsync()
    {
    }

    public virtual async Task AfterTestAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforePackageAsync()
    {
    }

    public virtual async Task AfterPackageAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforeDeployAsync()
    {
    }

    public virtual async Task AfterDeployAsync()
    {   
    }

    //-------------------------------------------------------------

    public virtual async Task BeforeFinalizeAsync()
    {
    }

    public virtual async Task AfterFinalizeAsync()
    {   
    }
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

    public BuildServerIntegration(ICakeContext cakeContext, Dictionary<string, object> parameters)
    {
        CakeContext = cakeContext;
        _parameters = parameters;

        _buildServers.Add(new ContinuaCIBuildServer(cakeContext));
    }

    public void SetBuildContext(BuildContext buildContext)
    {
        BuildContext = buildContext;

        foreach (var buildServer in _buildServers)
        {
            buildServer.SetBuildContext(buildContext);
        }   
    }

    public BuildContext BuildContext { get; private set; }

    public ICakeContext CakeContext { get; private set; }

    //-------------------------------------------------------------

    public async Task BeforeInitializeAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforeInitializeAsync();
        }        
    }

    public async Task AfterInitializeAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterInitializeAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforePrepareAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforePrepareAsync();
        }        
    }

    public async Task AfterPrepareAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterPrepareAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforeUpdateInfoAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforeUpdateInfoAsync();
        }        
    }

    public async Task AfterUpdateInfoAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterUpdateInfoAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforeBuildAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforeBuildAsync();
        }        
    }

    public async Task OnBuildFailedAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.OnBuildFailedAsync();
        }        
    }

    public async Task AfterBuildAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterBuildAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforeTestAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforeTestAsync();
        }        
    }

    public async Task OnTestFailedAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.OnTestFailedAsync();
        }        
    }

    public async Task AfterTestAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterTestAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforePackageAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforePackageAsync();
        }        
    }

    public async Task AfterPackageAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterPackageAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforeDeployAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforeDeployAsync();
        }        
    }

    public async Task AfterDeployAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterDeployAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task BeforeFinalizeAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.BeforeFinalizeAsync();
        }        
    }

    public async Task AfterFinalizeAsync()
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.AfterFinalizeAsync();
        }        
    }

    //-------------------------------------------------------------

    public async Task PinBuildAsync(string comment)
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.PinBuildAsync(comment);
        }        
    }

    //-------------------------------------------------------------

    public async Task SetVersionAsync(string version)
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.SetVersionAsync(version);
        }
    }

    //-------------------------------------------------------------

    public async Task SetVariableAsync(string variableName, string value)
    {
        foreach (var buildServer in _buildServers)
        {
            await buildServer.SetVariableAsync(variableName, value);
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

