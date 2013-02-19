using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

        foreach (var dependency in onlyBinaries)
        {
            var prefix = "";

            if (Unmanaged32Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(dependency)))
                prefix = "costura32.";
            if (Unmanaged64Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(dependency)))
                prefix = "costura64.";

            if (String.IsNullOrEmpty(prefix))
                continue;

            var fullPath = Path.GetFullPath(dependency);
            Embedd(prefix, fullPath);
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                Embedd(prefix, pdbFullPath);
            }
        }
    }

    IEnumerable<string> GetFilteredReferences(IEnumerable<string> onlyBinaries)
    {
        if (IncludeAssemblies.Any())
        {
            foreach (var file in onlyBinaries)
            {
                if (IncludeAssemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)) &&
                    !Unmanaged32Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)) &&
                    !Unmanaged64Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)))
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
                if (ExcludeAssemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)) ||
                    Unmanaged32Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)) ||
                    Unmanaged64Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)))
                {
                    continue;
                }
                yield return file;
            }
            yield break;
        }
        foreach (var file in onlyBinaries)
        {
            if (!Unmanaged32Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)) &&
                !Unmanaged64Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(file)))
            {
                yield return file;
            }
        }
    }

    void Embedd(string prefix, string fullPath)
    {
        var resourceName = String.Format("{0}{1}", prefix, Path.GetFileName(fullPath).ToLowerInvariant());
        if (ModuleDefinition.Resources.Any(x => x.Name == resourceName))
        {
            LogInfo(string.Format("\tSkipping '{0}' because it is already embedded", fullPath));
            return;
        }
        resourceName = String.Format("{0}cmp.{1}", prefix, Path.GetFileName(fullPath).ToLowerInvariant());
        LogInfo(string.Format("\tEmbedding '{0}'", fullPath));
        var memStream = new MemoryStream();
        using (var fileStream = File.OpenRead(fullPath))
            using (var compressedStream = new DeflateStream(memStream, CompressionMode.Compress, true))
            {
                fileStream.CopyTo(compressedStream);
            }
        memStream.Position = 0;
        streams.Add(memStream);
        var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, memStream);
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