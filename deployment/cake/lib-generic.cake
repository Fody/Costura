using System.Reflection;

//-------------------------------------------------------------

private static readonly Dictionary<string, bool> _dotNetCoreCache = new Dictionary<string, bool>();

//-------------------------------------------------------------

public interface IIntegration
{
    
}

//-------------------------------------------------------------

public abstract class IntegrationBase : IIntegration
{
    protected IntegrationBase(BuildContext buildContext)
    {
        BuildContext = buildContext;
    }

    public BuildContext BuildContext { get; private set; }
}

//-------------------------------------------------------------

public interface IProcessor
{
    bool HasItems();

    Task PrepareAsync();
    Task UpdateInfoAsync();
    Task BuildAsync();
    Task PackageAsync();
    Task DeployAsync();
    Task FinalizeAsync();
}

//-------------------------------------------------------------

public abstract class ProcessorBase : IProcessor
{   
    protected readonly BuildContext BuildContext; 
    protected readonly ICakeContext CakeContext;

    protected ProcessorBase(BuildContext buildContext)
    {
        BuildContext = buildContext;
        CakeContext = buildContext.CakeContext;

        Name = GetProcessorName();
    }

    public string Name { get; private set; }

    protected virtual string GetProcessorName()
    {
        var name = GetType().Name.Replace("Processor", string.Empty);
        return name;
    }

    public abstract bool HasItems();

    public abstract Task PrepareAsync();
    public abstract Task UpdateInfoAsync();
    public abstract Task BuildAsync();
    public abstract Task PackageAsync();
    public abstract Task DeployAsync();
    public abstract Task FinalizeAsync();
}

//-------------------------------------------------------------

public interface IBuildContext
{
    ICakeContext CakeContext { get; }
    IBuildContext ParentContext { get; }

    void Validate();
    void LogStateInfo();
}

//-------------------------------------------------------------

public abstract class BuildContextBase : IBuildContext
{
    private List<IBuildContext> _childContexts;
    private readonly string _contextName;

    protected BuildContextBase(ICakeContext cakeContext)
    {
        CakeContext = cakeContext;

        _contextName = GetContextName();
    }

    protected BuildContextBase(IBuildContext parentContext)
        : this(parentContext.CakeContext)
    {
        ParentContext = parentContext;
    }

    public ICakeContext CakeContext { get; private set; }

    public IBuildContext ParentContext { get; private set; }

    private List<IBuildContext> GetChildContexts()
    {
        var items = _childContexts;
        if (items is null)
        {
            items = new List<IBuildContext>();

            var properties = GetType().GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in properties)
            {
                //if (property.Name.EndsWith("Context"))
                if (property.PropertyType.GetInterfaces().Any(x => x == (typeof(IBuildContext))))
                {
                    items.Add((IBuildContext)property.GetValue(this, null));
                }
            }

            _childContexts = items;

            CakeContext.Debug($"Found '{items.Count}' child contexts for '{_contextName}' context");
        }

        return items;
    }

    protected virtual string GetContextName()
    {
        var name = GetType().Name.Replace("Context", string.Empty);
        return name;
    }

    public void Validate()
    {
        CakeContext.Information($"Validating '{_contextName}' context");

        ValidateContext();

        foreach (var childContext in GetChildContexts())
        {
            childContext.Validate();
        }
    }

    protected abstract void ValidateContext();

    public void LogStateInfo()
    {
        LogStateInfoForContext();

        foreach (var childContext in GetChildContexts())
        {
            childContext.LogStateInfo();
        }
    }

    protected abstract void LogStateInfoForContext();
}

//-------------------------------------------------------------

public abstract class BuildContextWithItemsBase : BuildContextBase
{
    protected BuildContextWithItemsBase(ICakeContext cakeContext)
        : base(cakeContext)
    {
    }

    protected BuildContextWithItemsBase(IBuildContext parentContext)
        : base(parentContext)
    {
    }

    public List<string> Items { get; set; }
}

//-------------------------------------------------------------

public enum TargetType
{
    Unknown,

    Component,

    DockerImage,

    GitHubPages,

    Tool,

    UwpApp,

    VsExtension,

    WebApp,

    WpfApp
}

//-------------------------------------------------------------

private static void LogSeparator(this ICakeContext cakeContext, string messageFormat, params object[] args)
{
    cakeContext.Information("");
    cakeContext.Information("--------------------------------------------------------------------------------");
    cakeContext.Information(messageFormat, args);
    cakeContext.Information("--------------------------------------------------------------------------------");
    cakeContext.Information("");
}

//-------------------------------------------------------------

private static void LogSeparator(this ICakeContext cakeContext)
{
    cakeContext.Information("");
    cakeContext.Information("--------------------------------------------------------------------------------");
    cakeContext.Information("");
}

//-------------------------------------------------------------

private static string GetTempDirectory(BuildContext buildContext, string section, string projectName)
{
    var tempDirectory = buildContext.CakeContext.Directory(string.Format("./temp/{0}/{1}", section, projectName));

    buildContext.CakeContext.CreateDirectory(tempDirectory);

    return tempDirectory;
}

//-------------------------------------------------------------

private static List<string> SplitCommaSeparatedList(string value)
{
    return SplitSeparatedList(value, ',');
}

//-------------------------------------------------------------

private static List<string> SplitSeparatedList(string value, params char[] separators)
{
    var list = new List<string>();
            
    if (!string.IsNullOrWhiteSpace(value))
    {
        var splitted = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var split in splitted)
        {
            list.Add(split.Trim());
        }
    }

    return list;
}

//-------------------------------------------------------------

private static void RestoreNuGetPackages(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName)
{
    buildContext.CakeContext.Information("Restoring packages for {0}", solutionOrProjectFileName);
    
    try
    {
        var nuGetRestoreSettings = new NuGetRestoreSettings
        {
        };

        var sources = SplitSeparatedList(buildContext.General.NuGet.PackageSources, ';');
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

private static void ConfigureMsBuild(BuildContext buildContext, MSBuildSettings msBuildSettings, 
    string projectName, string action = "build", bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(buildContext, allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        buildContext.CakeContext.Information($"Overriding ms build tool path to '{toolPath}'");

        msBuildSettings.ToolPath = toolPath;
    }

    // No NuGet restore (should already be done)
    msBuildSettings.WithProperty("ResolveNuGetPackages", "false");

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLogger
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.log", projectName, action))
    });

    // Enable for bin logging
    msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    {
        Enabled = true,
        Imports = MSBuildBinaryLogImports.Embed,
        FileName = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    };
}

//-------------------------------------------------------------

private static void ConfigureMsBuildForDotNetCore(BuildContext buildContext, DotNetCoreMSBuildSettings msBuildSettings, 
    string projectName, string action = "build", bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(buildContext, allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        buildContext.CakeContext.Information($"Overriding ms build tool path to '{toolPath}'");

        msBuildSettings.ToolPath = toolPath;
    }

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLoggerSettings
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.log", projectName, action))
    });

    // Enable for bin logging
    //msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    //{
    //    Enabled = true,
    //    Imports = MSBuildBinaryLogImports.Embed,
    //    FileName = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    //};
    
    // Note: this only works for direct .net core msbuild usage, not when this is
    // being wrapped in a tool (such as 'dotnet pack')
    var binLogArgs = string.Format("-bl:\"{0}\";ProjectImports=Embed", 
        System.IO.Path.Combine(buildContext.General.OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action)));

    msBuildSettings.ArgumentCustomization = args => args.Append(binLogArgs);
}

//-------------------------------------------------------------

private static string GetVisualStudioDirectory(BuildContext buildContext, bool? allowVsPrerelease = null)
{
    // TODO: Support different editions (e.g. Professional, Enterprise, Community, etc)

    if ((allowVsPrerelease ?? true) && buildContext.General.UseVisualStudioPrerelease)
    {
        buildContext.CakeContext.Debug("Checking for installation of Visual Studio 2019 preview");

        var pathFor2019Preview = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\";
        if (System.IO.Directory.Exists(pathFor2019Preview))
        {
           // Note: SonarQube supports VS 2019 now
           //buildContext.CakeContext.Information("Using Visual Studio 2019 preview, note that SonarQube will be disabled since it's not (yet) compatible with VS2019");
           //buildContext.General.SonarQube.IsDisabled = true;
           return pathFor2019Preview;
        }

        buildContext.CakeContext.Debug("Checking for installation of Visual Studio 2017 preview");

        var pathFor2017Preview = @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Professional\";
        if (System.IO.Directory.Exists(pathFor2017Preview))
        {
            buildContext.CakeContext.Information("Using Visual Studio 2017 preview");
            return pathFor2017Preview;
        }
    }
    
    buildContext.CakeContext.Debug("Checking for installation of Visual Studio 2019");

    var pathFor2019 = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\";
    if (System.IO.Directory.Exists(pathFor2019))
    {
       buildContext.CakeContext.Information("Using Visual Studio 2019");
       return pathFor2019;
    }
	
	var pathFor2019Community = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\";
    if (System.IO.Directory.Exists(pathFor2019Community))
    {
       buildContext.CakeContext.Information("Using Visual Studio 2019 CE");
       return pathFor2019Community;
    }

    buildContext.CakeContext.Debug("Checking for installation of Visual Studio 2017");

    var pathFor2017 = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\";
    if (System.IO.Directory.Exists(pathFor2017))
    {
        buildContext.CakeContext.Information("Using Visual Studio 2017");
        return pathFor2017;
    }

    // Failed
    return null;
}

//-------------------------------------------------------------

private static string GetVisualStudioPath(BuildContext buildContext, bool? allowVsPrerelease = null)
{
    var potentialPaths = new []
    {
        @"MSBuild\Current\Bin\msbuild.exe",
        @"MSBuild\15.0\Bin\msbuild.exe"
    };

    var directory = GetVisualStudioDirectory(buildContext, allowVsPrerelease);

    foreach (var potentialPath in potentialPaths)
    {
        var pathToCheck = string.Format(@"{0}\{1}", directory, potentialPath);
        if (System.IO.File.Exists(pathToCheck))
        {
            return pathToCheck;
        }
    }

    throw new Exception("Could not find the path to Visual Studio (msbuild.exe)");
}

//-------------------------------------------------------------

private static bool IsCppProject(string projectName)
{
    return projectName.EndsWith(".vcxproj");
}

//-------------------------------------------------------------

private static string GetProjectDirectory(string projectName)
{
    var projectDirectory = string.Format("./src/{0}/", projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private static string GetProjectOutputDirectory(BuildContext buildContext, string projectName)
{
    var projectDirectory = string.Format("{0}/{1}", buildContext.General.OutputRootDirectory, projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private static string GetProjectFileName(BuildContext buildContext, string projectName)
{
    var allowedExtensions = new [] 
    {
        "csproj",
        "vcxproj"
    };

    foreach (var allowedExtension in allowedExtensions)
    {
        var fileName = string.Format("{0}{1}.{2}", GetProjectDirectory(projectName), projectName, allowedExtension);

        //buildContext.CakeContext.Information(fileName);

        if (buildContext.CakeContext.FileExists(fileName))
        {
            return fileName;
        }
    }

    // Old behavior
    var fallbackFileName = string.Format("{0}{1}.{2}", GetProjectDirectory(projectName), projectName, allowedExtensions[0]);
    return fallbackFileName;
}

//-------------------------------------------------------------

private static string GetProjectSlug(string projectName)
{
    var slug = projectName.Replace(".", "").Replace(" ", "");
    return slug;
}

//-------------------------------------------------------------

private static string GetTargetSpecificConfigurationValue(BuildContext buildContext, TargetType targetType, string configurationPrefix, string fallbackValue)
{
    // Allow per project overrides via "[configurationPrefix][targetType]"
    var keyToCheck = string.Format("{0}{1}", configurationPrefix, targetType);

    var value = buildContext.BuildServer.GetVariable(keyToCheck, fallbackValue);
    return value;
}

//-------------------------------------------------------------

private static string GetProjectSpecificConfigurationValue(BuildContext buildContext, string projectName, string configurationPrefix, string fallbackValue)
{
    // Allow per project overrides via "[configurationPrefix][projectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("{0}{1}", configurationPrefix, slug);

    var value = buildContext.BuildServer.GetVariable(keyToCheck, fallbackValue);
    return value;
}

//-------------------------------------------------------------

private static bool IsDotNetCoreProject(BuildContext buildContext, string projectName)
{
    var projectFileName = GetProjectFileName(buildContext, projectName);

    if (!_dotNetCoreCache.TryGetValue(projectFileName, out var isDotNetCore))
    {
        isDotNetCore = false;

        var lines = System.IO.File.ReadAllLines(projectFileName);
        foreach (var line in lines)
        {
            // Match both *TargetFramework* and *TargetFrameworks* 
            var lowerCase = line.ToLower();
            if (lowerCase.Contains("targetframework"))
            {
                if (lowerCase.Contains("netcore"))
                {
                    isDotNetCore = true;
                    break;
                }
            }
        }

        _dotNetCoreCache[projectFileName] = isDotNetCore;
    }

    return _dotNetCoreCache[projectFileName];
}

//-------------------------------------------------------------

private static bool ShouldProcessProject(BuildContext buildContext, string projectName)
{
    // Includes > Excludes
    var includes = buildContext.General.Includes;
    if (includes.Count > 0)
    {
        var process = includes.Any(x => string.Equals(x, projectName, StringComparison.OrdinalIgnoreCase));

        if (!process)
        {
            buildContext.CakeContext.Warning("Project '{0}' should not be processed, removing from projects to process", projectName);
        }

        return process;
    }

    var excludes = buildContext.General.Excludes;
    if (excludes.Count > 0)
    {
        var process = !excludes.Any(x => string.Equals(x, projectName, StringComparison.OrdinalIgnoreCase));

        if (!process)
        {
            buildContext.CakeContext.Warning("Project '{0}' should not be processed, removing from projects to process", projectName);
        }

        return process;
    }

    return true;
}

//-------------------------------------------------------------

private static bool ShouldDeployProject(BuildContext buildContext, string projectName)
{
    // Allow the build server to configure this via "Deploy[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Deploy{0}", slug);

    var shouldDeploy = buildContext.BuildServer.GetVariableAsBool(keyToCheck, true);
    
    buildContext.CakeContext.Information("Value for '{0}': {1}", keyToCheck, shouldDeploy);

    return shouldDeploy;
}