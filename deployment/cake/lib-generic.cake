using System.Reflection;

//-------------------------------------------------------------

private static readonly Dictionary<string, bool> _dotNetCoreCache = new Dictionary<string, bool>();
private static readonly Dictionary<string, bool> _blazorCache = new Dictionary<string, bool>();

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

private static bool IsCppProject(string projectName)
{
    return projectName.EndsWith(".vcxproj");
}

//-------------------------------------------------------------

private static bool IsBlazorProject(BuildContext buildContext, string projectName)
{
    var projectFileName = GetProjectFileName(buildContext, projectName);

    if (!_blazorCache.TryGetValue(projectFileName, out var isBlazor))
    {
        isBlazor = false;

        var lines = System.IO.File.ReadAllLines(projectFileName);
        foreach (var line in lines)
        {
            // Match both *TargetFramework* and *TargetFrameworks* 
            var lowerCase = line.ToLower();
            if (lowerCase.Contains("<project"))
            {
                if (lowerCase.Contains("microsoft.net.sdk.razor"))
                {
                    isBlazor = true;
                    break;
                }
            }
        }

        _blazorCache[projectFileName] = isBlazor;
    }

    return _blazorCache[projectFileName];
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

private static bool ShouldProcessProject(BuildContext buildContext, string projectName, 
    bool checkDeployment = true)
{
    // If part of all projects, always include
    if (buildContext.AllProjects.Contains(projectName))
    {
        return true;
    }

    // Is this a dependency?
    if (buildContext.Dependencies.Items.Contains(projectName))
    {
        if (buildContext.Dependencies.ShouldBuildDependency(projectName))
        {
            return true;
        }
    }

    // Is this a test project?
    if (buildContext.Tests.Items.Contains(projectName))
    {
        // Assume false, the test processor will check for this
        return false;
    }

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

    // Is this a known project?
    if (!buildContext.RegisteredProjects.Any(x => string.Equals(projectName, x, StringComparison.OrdinalIgnoreCase)))
    {
        buildContext.CakeContext.Warning("Project '{0}' should not be processed, does not exist as registered project", projectName);
        return false;
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
        if (checkDeployment && 
            !ShouldPackageProject(buildContext, projectName) && 
            !ShouldDeployProject(buildContext, projectName))
        {
            buildContext.CakeContext.Warning("Project '{0}' should not be processed because this is not a CI build, does not contain tests and the project should not be deployed, removing from projects to process", projectName);
            return false;
        }
    //}

    return true;
}

//-------------------------------------------------------------

private static List<string> GetProjectRuntimesIdentifiers(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName, List<string> runtimeIdentifiersToInvestigate)
{
    var projectFileContents = System.IO.File.ReadAllText(solutionOrProjectFileName.FullPath)?.ToLower();

    var supportedRuntimeIdentifiers = new List<string>();

    foreach (var runtimeIdentifier in runtimeIdentifiersToInvestigate)
    {
        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            if (!projectFileContents.Contains(runtimeIdentifier.ToLower()))
            {
                buildContext.CakeContext.Information("Project '{0}' does not support runtime identifier '{1}', removing from supported runtime identifiers list", solutionOrProjectFileName, runtimeIdentifier);
                continue;
            }
        }

        supportedRuntimeIdentifiers.Add(runtimeIdentifier);
    }

    if (supportedRuntimeIdentifiers.Count == 0)
    {
        buildContext.CakeContext.Information("Project '{0}' does not have any explicit runtime identifiers left, adding empty one as default", solutionOrProjectFileName);

        // Default
        supportedRuntimeIdentifiers.Add(string.Empty);
    }

    return supportedRuntimeIdentifiers;
}

//-------------------------------------------------------------

private static bool ShouldBuildProject(BuildContext buildContext, string projectName)
{
    // Allow the build server to configure this via "Build[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Build{0}", slug);

    // Note: we return false by default. This method is only used to explicitly
    // force a build even when a project is not deployable
    var shouldBuild = buildContext.BuildServer.GetVariableAsBool(keyToCheck, false);

    buildContext.CakeContext.Information($"Value for '{keyToCheck}': {shouldBuild}");

    return shouldBuild;
}

//-------------------------------------------------------------

private static bool ShouldPackageProject(BuildContext buildContext, string projectName)
{
    // Allow the build server to configure this via "Package[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Package{0}", slug);

    var shouldPackage = buildContext.BuildServer.GetVariableAsBool(keyToCheck, true);

    // If this is *only* a dependency, it should never be deployed
    if (IsOnlyDependencyProject(buildContext, projectName))
    {
        shouldPackage = false;
    }

    if (shouldPackage && !ShouldProcessProject(buildContext, projectName, false))
    {
        buildContext.CakeContext.Information($"Project '{projectName}' should not be processed, excluding it anyway");
        
        shouldPackage = false;
    }

    buildContext.CakeContext.Information($"Value for '{keyToCheck}': {shouldPackage}");

    return shouldPackage;
}

//-------------------------------------------------------------

private static bool ShouldDeployProject(BuildContext buildContext, string projectName)
{
    // Allow the build server to configure this via "Deploy[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Deploy{0}", slug);

    var shouldDeploy = buildContext.BuildServer.GetVariableAsBool(keyToCheck, true);

    // If this is *only* a dependency, it should never be deployed
    if (IsOnlyDependencyProject(buildContext, projectName))
    {
        shouldDeploy = false;
    }

    if (shouldDeploy && !ShouldProcessProject(buildContext, projectName, false))
    {
        buildContext.CakeContext.Information($"Project '{projectName}' should not be processed, excluding it anyway");
        
        shouldDeploy = false;
    }

    buildContext.CakeContext.Information($"Value for '{keyToCheck}': {shouldDeploy}");

    return shouldDeploy;
}

//-------------------------------------------------------------

private static bool IsOnlyDependencyProject(BuildContext buildContext, string projectName)
{
    buildContext.CakeContext.Information($"Checking if project '{projectName}' is a dependency only");

    // If not in the dependencies list, we can stop checking
    if (!buildContext.Dependencies.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is not in list of dependencies, assuming not dependency only");
        return false;
    }

    if (buildContext.Components.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of components, assuming not dependency only");
        return false;
    }

    if (buildContext.DockerImages.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of docker images, assuming not dependency only");
        return false;
    }

    if (buildContext.GitHubPages.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of GitHub pages, assuming not dependency only");
        return false;
    }

    if (buildContext.Templates.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of templates, assuming not dependency only");
        return false;
    }

    if (buildContext.Tools.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of tools, assuming not dependency only");
        return false;
    }            

    if (buildContext.Uwp.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of UWP apps, assuming not dependency only");
        return false;
    }   

    if (buildContext.VsExtensions.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of VS extensions, assuming not dependency only");
        return false;
    }   

    if (buildContext.Web.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of web apps, assuming not dependency only");
        return false;
    }  

    if (buildContext.Wpf.Items.Contains(projectName))
    {
        buildContext.CakeContext.Information($"Project is list of WPF apps, assuming not dependency only");
        return false;
    }  

    buildContext.CakeContext.Information($"Project '{projectName}' is a dependency only");

    // It's in the dependencies list and not in any other list
    return true;
}

//-------------------------------------------------------------

public static void Add(this Dictionary<string, List<string>> dictionary, string project, params string[] projects)
{
    dictionary.Add(project, new List<string>(projects));
}