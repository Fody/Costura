using System;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

public partial class ModuleWeaver
{
    private void ProcessNativeResources(bool compress)
    {
        var unprocessedNameMatch = new Regex(@"^(.*\.)?costura(32|64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var processedNameMatch = new Regex(@"^costura(32|64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (var resource in ModuleDefinition.Resources.OfType<EmbeddedResource>())
        {
            var match = unprocessedNameMatch.Match(resource.Name);
            if (match.Success)
            {
                resource.Name = resource.Name.Substring(match.Groups[1].Length).ToLowerInvariant();
                _hasUnmanaged = true;
            }

            if (processedNameMatch.IsMatch(resource.Name))
            {
                using (var stream = resource.GetResourceStream())
                {
                    if (compress && resource.Name.EndsWith(".compressed",StringComparison.OrdinalIgnoreCase))
                    {
                        using (var compressStream = new DeflateStream(stream, CompressionMode.Decompress))
                        {
                            _checksums.Add(resource.Name, CalculateChecksum(compressStream));
                        }
                    }
                    else
                    {
                        _checksums.Add(resource.Name, CalculateChecksum(stream));
                    }
                }
            }
        }
    }
}