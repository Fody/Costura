using System;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

public partial class ModuleWeaver
{
    private void ProcessNativeResources(bool compress)
    {
        var unprocessedNameMatchBackwardsCompatibility = new Regex(@"^(.*\.)?costura(32|64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var unprocessedNameMatch = new Regex(@"^(.*\.)?costura_(win|linux|osx)_(x86|x64|Arm64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        var processedNameMatchBackwardsCompatibility = new Regex(@"^costura(32|64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var processedNameMatch = new Regex(@"^costura_(win|linux|osx)_(x86|x64|arm64)\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (var resource in ModuleDefinition.Resources.OfType<EmbeddedResource>())
        {
            if (unprocessedNameMatchBackwardsCompatibility.IsMatch(resource.Name))
            {
                WriteError($"Please use the new folder structure (e.g. 'costura-win-x86')");
                continue;
            }

            var match = unprocessedNameMatch.Match(resource.Name);
            if (match.Success)
            {
                resource.Name = resource.Name.Substring(match.Groups[1].Length).ToLowerInvariant();
                _hasUnmanaged = true;
            }

            if (processedNameMatchBackwardsCompatibility.IsMatch(resource.Name))
            {
                WriteError($"Please use the new folder structure (e.g. 'costura-win-x86')");
                continue;
            }

            if (processedNameMatch.IsMatch(resource.Name))
            {
                using (var stream = resource.GetResourceStream())
                {
                    if (compress && resource.Name.EndsWith(".compressed",StringComparison.OrdinalIgnoreCase))
                    {
                        using (var compressStream = new DeflateStream(stream, CompressionMode.Decompress))
                        {
                            _checksums.Add(resource.Name, CalculateSha1Checksum(compressStream));
                        }
                    }
                    else
                    {
                        _checksums.Add(resource.Name, CalculateSha1Checksum(stream));
                    }
                }
            }
        }
    }
}
