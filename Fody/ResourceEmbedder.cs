using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

public partial class ModuleWeaver : IDisposable
{
    List<Stream> streams = new List<Stream>();

    public void EmbedResources()
    {
        if (ReferenceCopyLocalPaths == null)
        {
            throw new WeavingException("ReferenceCopyLocalPaths is required you may need to update to the latest version of Fody.");
        }
        foreach (var dependency in GetFilteredReferences())
        {
            var fullPath = Path.GetFullPath(dependency);
            Embedd(fullPath);
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                Embedd(pdbFullPath);
            }
        }
    }

    IEnumerable<string> GetFilteredReferences()
    {
        var onlyBinaries = ReferenceCopyLocalPaths.Where(x => x.EndsWith(".dll") || x.EndsWith(".exe"));
        if (IncludeAssemblies.Any())
        {
            foreach (var file in onlyBinaries)
            {
                if (IncludeAssemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)))
                {
                    yield return file;
                }
            }
            yield break;
        }
        if (ExcludeAssemblies.Any())
        {
            foreach (var file in onlyBinaries)
            {
                if (ExcludeAssemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)))
                {
                    continue;
                }
                yield return file;
            }
            yield break;
        }
        foreach (var file in onlyBinaries)
        {
            yield return file;
        }
    }

    void Embedd(string fullPath)
    {
        var resourceName = "costura." + Path.GetFileName(fullPath).ToLowerInvariant();
        if (ModuleDefinition.Resources.Any(x => x.Name == resourceName))
        {
            LogInfo(string.Format("\tSkipping '{0}' because it is already embedded", fullPath));
            return;
        }
        LogInfo(string.Format("\tEmbedding '{0}'", fullPath));
        var fileStream = File.OpenRead(fullPath);
        streams.Add(fileStream);
        var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, fileStream);
        ModuleDefinition.Resources.Add(resource);
    }

    public void Dispose()
    {
        if (streams == null)
        {
            return;
        }
        foreach (var stream in streams)
        {
            stream.Dispose();
        }
    }
}