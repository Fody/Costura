using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

public partial class ModuleWeaver : IDisposable
{
    List<Stream> streams = new List<Stream>();
    string cachePath;

    public void EmbedResources()
    {
        if (ReferenceCopyLocalPaths == null)
        {
            throw new WeavingException("ReferenceCopyLocalPaths is required you may need to update to the latest version of Fody.");
        }

        cachePath = Path.Combine(Path.GetDirectoryName(AssemblyFilePath), "Costura");
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }

        // Ignore resource assemblies for now
        var onlyBinaries = ReferenceCopyLocalPaths.Where(x => x.EndsWith(".dll") || x.EndsWith(".exe"));

        foreach (var dependency in GetFilteredReferences(onlyBinaries))
        {
            var fullPath = Path.GetFullPath(dependency);

            if (dependency.EndsWith(".resources.dll"))
            {
                // TODO support resources
                //Embedd(string.Format("costura.{0}.", Path.GetFileName(Path.GetDirectoryName(fullPath))), fullPath);
                continue;
            }

            Embed("costura.", fullPath);
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                Embed("costura.", pdbFullPath);
            }
        }

        foreach (var dependency in onlyBinaries)
        {
            var prefix = "";

            if (Unmanaged32Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(dependency)))
            {
                prefix = "costura32.";
                HasUnmanaged = true;
            }
            if (Unmanaged64Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(dependency)))
            {
                prefix = "costura64.";
                HasUnmanaged = true;
            }

            if (String.IsNullOrEmpty(prefix))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(dependency);
            var resourceName = Embed(prefix, fullPath);
            checksums.Add(resourceName, CalculateChecksum(fullPath));
            if (!IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                resourceName = Embed(prefix, pdbFullPath);
                checksums.Add(resourceName, CalculateChecksum(pdbFullPath));
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
                    Unmanaged32Assemblies.All(x => x != Path.GetFileNameWithoutExtension(file)) &&
                    Unmanaged64Assemblies.All(x => x != Path.GetFileNameWithoutExtension(file)))
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
            if (Unmanaged32Assemblies.All(x => x != Path.GetFileNameWithoutExtension(file)) &&
                Unmanaged64Assemblies.All(x => x != Path.GetFileNameWithoutExtension(file)))
            {
                yield return file;
            }
        }
    }

    string Embed(string prefix, string fullPath)
    {
        var resourceName = String.Format("{0}{1}", prefix, Path.GetFileName(fullPath).ToLowerInvariant());
        if (ModuleDefinition.Resources.Any(x => x.Name == resourceName))
        {
            LogInfo(string.Format("\tSkipping '{0}' because it is already embedded", fullPath));
            return resourceName;
        }

        if (!DisableCompression)
        {
            resourceName = String.Format("{0}{1}.zip", prefix, Path.GetFileName(fullPath).ToLowerInvariant());
        }

        LogInfo(string.Format("\tEmbedding '{0}'", fullPath));

        var checksum = CalculateChecksum(fullPath);
        var cacheFile = Path.Combine(cachePath, String.Format("{0}.{1}", checksum, resourceName));
        var memoryStream = new MemoryStream();

        if (File.Exists(cacheFile))
        {
            using (var fileStream = File.Open(cacheFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fileStream.CopyTo(memoryStream);
            }
        }
        else
        {
            using (var fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (!DisableCompression)
                {
                    using (var compressedStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
                    {
                        fileStream.CopyTo(compressedStream);
                    }
                }
                else
                {
                    fileStream.CopyTo(memoryStream);
                }
            }
            memoryStream.Position = 0;
            using (var fileStream = File.Open(cacheFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                memoryStream.CopyTo(fileStream);
            }
        }
        memoryStream.Position = 0;
        streams.Add(memoryStream);
        var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, memoryStream);
        ModuleDefinition.Resources.Add(resource);

        return resourceName;
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