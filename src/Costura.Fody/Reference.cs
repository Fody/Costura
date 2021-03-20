using System;
using System.ComponentModel;
using System.IO;
using Fody;

public class Reference
{
    // Path such as "C:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder\\runtime.win-arm64.runtime.native.system.data.sqlclient.sni\\4.4.0\\runtimes\\win-arm64\\native\\sni.dll"

    public Reference(string fullPath, bool useRuntimeReferencePaths)
    {
        if (!Path.IsPathRooted(fullPath))
        {
            throw new WeavingException($"Only full paths are supported, please fix in Costura so '{fullPath}' can be included");
        }

        FullPath = fullPath;
        IsCopyLocal = true;

        FileName = Path.GetFileName(fullPath);
        FileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        Directory = Path.GetDirectoryName(fullPath);

        CalculateRelativeFileName(useRuntimeReferencePaths);
    }

    public string FullPath { get; private set; }

    public string FileName { get; private set; }

    public string FileNameWithoutExtension { get; private set; }

    public string Directory { get; private set; }

    public string RelativeFileName { get; private set; }

    public string RelativePrefix { get; private set; }

    public bool IsCopyLocal { get; set; }

    public bool IsResourcesAssembly { get; private set; }

    public bool IsRuntimeReference { get; private set; }

    public string PredictedResourceName { get; private set; }

    private void CalculateRelativeFileName(bool useRuntimeReferencePaths)
    {
        const string RuntimesFolderName = "runtimes";

        var relativeFileName = Path.GetFileName(FullPath);

        var parentDirectory = Path.GetDirectoryName(FullPath);
        var directoryName = Path.GetFileName(parentDirectory);

        // Search either for "lib" or "runtimes"

        while (!directoryName.Equals(RuntimesFolderName, StringComparison.OrdinalIgnoreCase))
        {
            relativeFileName = $"{directoryName}/{relativeFileName}";

            var newParentDirectory = Path.GetDirectoryName(parentDirectory);
            if (newParentDirectory is null || newParentDirectory.Equals(parentDirectory))
            {
                // Equal, we are in a loop, break
                break;
            }

            parentDirectory = newParentDirectory;
            directoryName = Path.GetFileName(parentDirectory);
        }

        IsResourcesAssembly = relativeFileName.EndsWith("resources.dll");
        IsRuntimeReference = useRuntimeReferencePaths && directoryName.Equals(RuntimesFolderName, StringComparison.OrdinalIgnoreCase);

        if (IsRuntimeReference)
        {
            relativeFileName = $"{RuntimesFolderName}/{relativeFileName}";
        }
        else
        {
            // Library, use filename only
            relativeFileName = Path.GetFileName(FullPath);
        }

        if (IsResourcesAssembly && !IsRuntimeReference)
        {
            // We need the relative path (e.g. /nl/Catel.Core.dll)
            parentDirectory = Path.GetDirectoryName(FullPath);
            directoryName = Path.GetFileName(parentDirectory);

            relativeFileName = $"{directoryName}/{relativeFileName}";
        }
        
        RelativeFileName = relativeFileName;
        RelativePrefix = IsRuntimeReference ? Path.GetDirectoryName(relativeFileName).Replace("/", ".").Replace("\\", ".") : string.Empty;
        PredictedResourceName = $"{GetResourceNamePrefix("costura.")}{FileName.ToLowerInvariant()}";
    }

    public string GetResourceNamePrefix(string prefix)
    {
        var resourceName = prefix;
        if (!resourceName.EndsWith("."))
        {
            resourceName += ".";
        }

        if (!string.IsNullOrWhiteSpace(RelativePrefix))
        {
            resourceName += $"{RelativePrefix}.";
        }

        if (IsResourcesAssembly)
        {
            resourceName += Path.GetFileName(Path.GetDirectoryName(FullPath));
        }

        // File name will be added later, this is "just" a prefix
        //resourceName += $".{FileName}";

        if (!resourceName.EndsWith("."))
        {
            resourceName += ".";
        }

        resourceName = resourceName.ToLowerInvariant();

        return resourceName;
    }

    public override string ToString()
    {
        return $"{RelativeFileName} (Predicted resource name: {PredictedResourceName})";
    }
}
