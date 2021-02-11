#l "templates-variables.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"

using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.IO;

//-------------------------------------------------------------

public class TemplatesProcessor : ProcessorBase
{
    public TemplatesProcessor(BuildContext buildContext)
        : base(buildContext)
    {
        var templatesRelativePath = "./deployment/templates";

        if (CakeContext.DirectoryExists(templatesRelativePath))
        {
            var currentDirectoryPath = System.IO.Directory.GetCurrentDirectory();
            var templateAbsolutePath = System.IO.Path.Combine(currentDirectoryPath, templatesRelativePath);
            var files = System.IO.Directory.GetFiles(templateAbsolutePath, "*.*", System.IO.SearchOption.AllDirectories);
            
            CakeContext.Information($"Found '{files.Count()}' template files");

            foreach (var file in files)
            {              
                BuildContext.Templates.Items.Add(file.Substring(templateAbsolutePath.Length + 1));
            }
        }
    }

    public override bool HasItems()
    {
        return BuildContext.Templates.Items.Count > 0;
    }

    public override async Task PrepareAsync()
    {

    }

    public override async Task UpdateInfoAsync()
    {
        if (!HasItems())
        {
            return;
        }

        var variableRegex = new Regex(@"\$\{([^}]+)\}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        
        foreach (var template in BuildContext.Templates.Items)
        {
            CakeContext.Information($"Updating template file '{template}'");
            
            var templateSourceFile  = $"./deployment/templates/{template}";
            var content = CakeContext.FileReadText(templateSourceFile);

            var matches = variableRegex.Matches(content);

            foreach (var match in matches.Cast<Match>())
            {
                var variableName = match.Groups[1].Value;

                CakeContext.Information($"Found usage of variable '{variableName}'");

                if (!BuildContext.Variables.TryGetValue(variableName, out var replacement))
                {
                    CakeContext.Error($"Could not find value for variable '{variableName}'");
                    continue;   
                }
                
                content = content.Replace($"${{{variableName}}}", replacement);
            }

            CakeContext.FileWriteText($"{template}", content);
        }        
    }

    public override async Task BuildAsync()
    {
        // Run templates every time
        await UpdateInfoAsync();
    }

    public override async Task PackageAsync()
    {
        // Run templates every time
        await UpdateInfoAsync();
    }

    public override async Task DeployAsync()
    {
        if (!HasItems())
        {
            return;
        }
    }

    public override async Task FinalizeAsync()
    {

    }
}
