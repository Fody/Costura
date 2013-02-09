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

        var onlyBinaries = ReferenceCopyLocalPaths.Where(x => x.EndsWith(".dll") || x.EndsWith(".exe"));

        foreach (var dependency in GetFilteredReferences(onlyBinaries))
        {
            var fullPath = Path.GetFullPath(dependency);
            Embedd("costura.", fullPath);
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                Embedd("costura.", pdbFullPath);
            }
        }

        foreach (var dependency in onlyBinaries.Intersect(Unmanaged32Assemblies))
        {
            var fullPath = Path.GetFullPath(dependency);
            Embedd("costura32.", fullPath);
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                Embedd("costura32.", pdbFullPath);
            }
        }

        foreach (var dependency in onlyBinaries.Intersect(Unmanaged64Assemblies))
        {
            var fullPath = Path.GetFullPath(dependency);
            Embedd("costura64.", fullPath);
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                Embedd("costura64.", pdbFullPath);
            }
        }
    }

    IEnumerable<string> GetFilteredReferences(IEnumerable<string> onlyBinaries)
    {
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
            foreach (var file in onlyBinaries.Except(Unmanaged32Assemblies).Except(Unmanaged64Assemblies))
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

    void Embedd(string prefix, string fullPath)
    {
        var resourceName = prefix + Path.GetFileName(fullPath).ToLowerInvariant();
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