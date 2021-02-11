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

private static bool IsCppProject(string projectName)
{
    return projectName.EndsWith(".vcxproj");
}

//-------------------------------------------------------------

private static string GetProjectDirectory(string projectName)
{
    var projectDirectory = System.IO.Path.Combine(".", "src", projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private static string GetProjectOutputDirectory(BuildContext buildContext, string projectName)
{
    var projectDirectory = System.IO.Path.Combine(buildContext.General.OutputRootDirectory, projectName);
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
        var fileName = System.IO.Path.Combine(GetProjectDirectory(projectName), $"{projectName}.{allowedExtension}");
       
        //buildContext.CakeContext.Information(fileName);

        if (buildContext.CakeContext.FileExists(fileName))
        {
            return fileName;
        }
    }

    // Old behavior
    var fallbackFileName = System.IO.Path.Combine(GetProjectDirectory(projectName), $"{projectName}.{allowedExtensions[0]}");
    return fallbackFileName;
}

//-------------------------------------------------------------

private static string GetProjectSlug(string projectName, string replacement = "")
{
    var slug = projectName.Replace(".", replacement).Replace(" ", replacement);
    return slug;
}

//-------------------------------------------------------------

private static string[] GetTargetFrameworks(BuildContext buildContext, string projectName)
{
    var targetFrameworks = new List<string>();

    var projectFileName = GetProjectFileName(buildContext, projectName);
    var projectFileContents = System.IO.File.ReadAllText(projectFileName);

    var xmlDocument = XDocument.Parse(projectFileContents);
    var projectElement = xmlDocument.Root;

    foreach (var propertyGroupElement in projectElement.Elements("PropertyGroup"))
    {
        // Step 1: check TargetFramework
        var targetFrameworkElement = projectElement.Element("TargetFramework");
        if (targetFrameworkElement != null)
        {
            targetFrameworks.Add(targetFrameworkElement.Value);
            break;
        }

        // Step 2: check TargetFrameworks
        var targetFrameworksElement = propertyGroupElement.Element("TargetFrameworks");
        if (targetFrameworksElement != null)
        {
            var value = targetFrameworksElement.Value;
            targetFrameworks.AddRange(value.Split(new [] { ';' }));
            break;
        }
    }

    if (targetFrameworks.Count == 0)
    {
        throw new Exception(string.Format("No target frameworks could be detected for project '{0}'", projectName));
    }

    return targetFrameworks.ToArray();
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

private static void CleanProject(BuildContext buildContext, string projectName)
{
    buildContext.CakeContext.LogSeparator("Cleaning project '{0}'", projectName);

    var projectDirectory = GetProjectDirectory(projectName);

    buildContext.CakeContext.Information($"Investigating paths to clean up in '{projectDirectory}'");

    var directoriesToDelete = new List<string>();

    var binDirectory = System.IO.Path.Combine(projectDirectory, "bin");
    directoriesToDelete.Add(binDirectory);

    var objDirectory = System.IO.Path.Combine(projectDirectory, "obj");
    directoriesToDelete.Add(objDirectory);

    // Special C++ scenarios
    var projectFileName = GetProjectFileName(buildContext, projectName);
    if (IsCppProject(projectFileName))
    {
        var debugDirectory = System.IO.Path.Combine(projectDirectory, "Debug");
        directoriesToDelete.Add(debugDirectory);

        var releaseDirectory = System.IO.Path.Combine(projectDirectory, "Release");
        directoriesToDelete.Add(releaseDirectory);

        var x64Directory = System.IO.Path.Combine(projectDirectory, "x64");
        directoriesToDelete.Add(x64Directory);

        var x86Directory = System.IO.Path.Combine(projectDirectory, "x86");
        directoriesToDelete.Add(x86Directory);
    }

    foreach (var directoryToDelete in directoriesToDelete)
    {
        DeleteDirectoryWithLogging(buildContext, directoryToDelete);
    }
}

//-------------------------------------------------------------

private static void DeleteDirectoryWithLogging(BuildContext buildContext, string directoryToDelete)
{
    if (buildContext.CakeContext.DirectoryExists(directoryToDelete))
    {
        buildContext.CakeContext.Information($"Cleaning up directory '{directoryToDelete}'");

        buildContext.CakeContext.DeleteDirectory(directoryToDelete, new DeleteDirectorySettings
        {
            Force = true,
            Recursive = true
        });
    }
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

                if (lowerCase.Contains("net5") ||
                    lowerCase.Contains("net6") ||
                    lowerCase.Contains("net7") ||
                    lowerCase.Contains("net8"))
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

private static bool ShouldProcessProject(BuildContext buildContext, string projectName, bool checkDeployment = true)
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

    if (buildContext.General.IsCiBuild)
    {
        // In CI builds, we always want to include all projects
        return true;
    }

    if (ShouldBuildProject(buildContext, projectName))
    {
        // Always build
        return true;
    }

    // Experimental mode where we ignore projects that are not on the deploy list when not in CI mode, but
    // it can only work if they are not part of unit tests (but that should never happen)
    // if (buildContext.Tests.Items.Count == 0)
    // {
        if (checkDeployment && !ShouldDeployProject(buildContext, projectName))
        {
            buildContext.CakeContext.Warning("Project '{0}' should not be processed because this is not a CI build, does not contain tests and the project should not be deployed, removing from projects to process", projectName);
            return false;
        }
    //}

    return true;
}

//-------------------------------------------------------------

private static bool ShouldBuildProject(BuildContext buildContext, string projectName)
{
        // Allow the build server to configure this via "Build[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Build{0}", slug);

    var shouldBuild = buildContext.BuildServer.GetVariableAsBool(keyToCheck, true);

    buildContext.CakeContext.Information($"Value for '{keyToCheck}': {shouldBuild}");

    return shouldBuild;
}

//-------------------------------------------------------------

private static bool ShouldDeployProject(BuildContext buildContext, string projectName)
{
    // Allow the build server to configure this via "Deploy[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Deploy{0}", slug);

    var shouldDeploy = buildContext.BuildServer.GetVariableAsBool(keyToCheck, true);
    if (shouldDeploy && !ShouldProcessProject(buildContext, projectName, false))
    {
        buildContext.CakeContext.Information($"Project '{projectName}' should not be processed, excluding it anyway");
        
        shouldDeploy = false;
    }

    buildContext.CakeContext.Information($"Value for '{keyToCheck}': {shouldDeploy}");

    return shouldDeploy;
}