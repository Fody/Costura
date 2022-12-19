#l "buildserver.cake"

//-------------------------------------------------------------

public class DependenciesContext : BuildContextWithItemsBase
{
    public DependenciesContext(IBuildContext parentBuildContext, Dictionary<string, List<string>> dependencies)
        : base(parentBuildContext)
    {
        Dependencies = dependencies ?? new Dictionary<string, List<string>>();
        Items = Dependencies.Keys.ToList();
    }

    public Dictionary<string, List<string>> Dependencies { get; private set; }

    protected override void ValidateContext()
    {

    }
    
    protected override void LogStateInfoForContext()
    {
        CakeContext.Information($"Found '{Items.Count}' dependency projects");
    }

    public bool ShouldBuildDependency(string dependencyProject)
    {
        return ShouldBuildDependency(dependencyProject, Array.Empty<string>());
    }

    public bool ShouldBuildDependency(string dependencyProject, IEnumerable<string> knownDependenciesToBeBuilt)
    {
        if (!Dependencies.TryGetValue(dependencyProject, out var dependencyInfo))
        {
            return false;
        }

        if (dependencyInfo.Count == 0)
        {
            // No explicit projects defined, always build dependency
            return true;
        }

        foreach (var projectRequiringDependency in dependencyInfo)
        {
             CakeContext.Information($"Checking whether '{projectRequiringDependency}' is in the list to be processed");

            // Check dependencies of dependencies
            if (knownDependenciesToBeBuilt.Any(x => string.Equals(x, projectRequiringDependency, StringComparison.OrdinalIgnoreCase)))
            {
                CakeContext.Information($"Dependency '{dependencyProject}' is a dependency of dependency project '{projectRequiringDependency}', including this in the build");
                return true;
            }

            // Special case: *if* this is the 2nd round we check, and the project requiring this dependency is a test project,
            // we should check whether the test project is not already excluded. If so, the Deploy[SomeProject]Tests will return true
            // and this logic will still include it, so we need to exclude it explicitly
            if (IsTestProject((BuildContext)ParentContext, projectRequiringDependency) &&
                !knownDependenciesToBeBuilt.Contains(projectRequiringDependency))
            {
                CakeContext.Information($"Dependency '{dependencyProject}' is a dependency of '{projectRequiringDependency}', but that is an already excluded test project, not yet including in the build");

                // Important: don't return, there might be other projects
                continue;
            }

            // Check if we should build this project
            if (ShouldProcessProject((BuildContext)ParentContext, projectRequiringDependency))
            {
                CakeContext.Information($"Dependency '{dependencyProject}' is a dependency of '{projectRequiringDependency}', including this in the build");
                return true;
            }
        }

        return false;
    }
}

//-------------------------------------------------------------

private DependenciesContext InitializeDependenciesContext(BuildContext buildContext, IBuildContext parentBuildContext)
{
    var data = new DependenciesContext(parentBuildContext, Dependencies);

    return data;
}

//-------------------------------------------------------------

Dictionary<string, List<string>> _dependencies;

public Dictionary<string, List<string>> Dependencies
{
    get 
    {
        if (_dependencies is null)
        {
            _dependencies = new Dictionary<string, List<string>>();
        }

        return _dependencies;
    }
}