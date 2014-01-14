using System;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

partial class ModuleWeaver
{
    void ProcessNativeResources()
    {
        var unprocessedNameMatch = new Regex(@"^(.*\.)?costura(32|64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var processedNameMatch = new Regex(@"^costura(32|64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (var resource in ModuleDefinition.Resources.OfType<EmbeddedResource>())
        {
            var match = unprocessedNameMatch.Match(resource.Name);
            if (match.Success)
            {
                resource.Name = resource.Name.Substring(match.Groups[1].Length).ToLowerInvariant();
                hasUnmanaged = true;
            }

            if (processedNameMatch.IsMatch(resource.Name))
            {
                checksums.Add(resource.Name, CalculateChecksum(resource.GetResourceStream()));
            }
        }
    }
}