public static bool IsSourceLinkSupported(BuildContext buildContext, string projectName, string projectFileName)
{
    if (buildContext.General.SourceLink.IsDisabled)
    {
        return false;
    }
    
    if (buildContext.General.IsLocalBuild)
    {
        return false;
    }

    if (string.IsNullOrWhiteSpace(buildContext.General.Repository.Url))
    {
        return false;
    }

    // Only support C# projects
    if (!projectFileName.EndsWith(".csproj"))
    {
        return false;
    }

    // Is this a test project?
    if (buildContext.Tests.Items.Contains(projectName))
    {
        return false;
    }

    // Only support when running a real build, e.g. ot for 'Package' only
    if (!buildContext.General.Target.ToLower().Contains("build"))
    {
        return false;
    }

    return true;
}

//-------------------------------------------------------------

public static void InjectSourceLinkInProjectFile(BuildContext buildContext, string projectName, string projectFileName)
{
    try
    {
        // Only support C# projects
        if (!IsSourceLinkSupported(buildContext, projectName, projectFileName))
        {
            return;
        }

        // For SourceLink to work, the .csproj should contain something like this:
        // <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0-beta-63127-02" PrivateAssets="all" />
        var projectFileContents = System.IO.File.ReadAllText(projectFileName);
        if (projectFileContents.Contains("Microsoft.SourceLink.GitHub"))
        {
            return;
        }

        buildContext.CakeContext.Warning("No SourceLink reference found, automatically injecting SourceLink package reference now");

        //const string MSBuildNS = (XNamespace) "http://schemas.microsoft.com/developer/msbuild/2003";

        var xmlDocument = XDocument.Parse(projectFileContents);
        var projectElement = xmlDocument.Root;

        // Item group with package reference
        var referencesItemGroup = new XElement("ItemGroup");
        var sourceLinkPackageReference = new XElement("PackageReference");
        sourceLinkPackageReference.Add(new XAttribute("Include", "Microsoft.SourceLink.GitHub"));
        sourceLinkPackageReference.Add(new XAttribute("Version", "1.1.1"));
        sourceLinkPackageReference.Add(new XAttribute("PrivateAssets", "all"));

        referencesItemGroup.Add(sourceLinkPackageReference);
        projectElement.Add(referencesItemGroup);

        // Item group with source root
        // <SourceRoot Include="{repository root}" RepositoryUrl="{repository url}"/>
        var sourceRootItemGroup = new XElement("ItemGroup");
        var sourceRoot = new XElement("SourceRoot");

        // Required to end with a \
        var sourceRootValue = buildContext.General.RootDirectory;
        var directorySeparator = System.IO.Path.DirectorySeparatorChar.ToString();
        if (!sourceRootValue.EndsWith(directorySeparator))
        {
            sourceRootValue += directorySeparator;
        };

        sourceRoot.Add(new XAttribute("Include", sourceRootValue));
        sourceRoot.Add(new XAttribute("RepositoryUrl", buildContext.General.Repository.Url));

        // Note: since we are not allowing source control manager queries (we don't want to require a .git directory),
        // we must specify the additional information below
        sourceRoot.Add(new XAttribute("SourceControl", "git"));
        sourceRoot.Add(new XAttribute("RevisionId", buildContext.General.Repository.CommitId));

        sourceRootItemGroup.Add(sourceRoot);
        projectElement.Add(sourceRootItemGroup);

        xmlDocument.Save(projectFileName);
    }
    catch (Exception ex)
    {
        buildContext.CakeContext.Error($"Failed to process source link for project '{projectFileName}': {ex.Message}");
    }
}