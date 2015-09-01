using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver : IDisposable
{
    readonly List<Stream> streams = new List<Stream>();
    string cachePath;

    private void EmbedResources(Configuration config)
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

        var assembliesAdded = false;

        var onlyBinaries = ReferenceCopyLocalPaths.Where(x => x.EndsWith(".dll") || x.EndsWith(".exe"));

        foreach (var dependency in GetFilteredReferences(onlyBinaries, config))
        {
            var fullPath = Path.GetFullPath(dependency);

            string resourceName;

            if (dependency.EndsWith(".resources.dll"))
            {
                resourceName = Embed(string.Format("costura.{0}.", Path.GetFileName(Path.GetDirectoryName(fullPath))), fullPath, !config.DisableCompression);
                if (config.CreateTemporaryAssemblies)
                {
                    checksums.Add(resourceName, CalculateChecksum(fullPath));
                }
                continue;
            }

            resourceName = Embed("costura.", fullPath, !config.DisableCompression);
            assembliesAdded = true;

            if (config.CreateTemporaryAssemblies)
            {
                checksums.Add(resourceName, CalculateChecksum(fullPath));
            }
            if (!config.IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                resourceName = Embed("costura.", pdbFullPath, !config.DisableCompression);
                assembliesAdded = true;

                if (config.CreateTemporaryAssemblies)
                {
                    checksums.Add(resourceName, CalculateChecksum(pdbFullPath));
                }
            }
        }

        foreach (var dependency in onlyBinaries)
        {
            var prefix = "";

            if (config.Unmanaged32Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(dependency)))
            {
                prefix = "costura32.";
                hasUnmanaged = true;
            }
            if (config.Unmanaged64Assemblies.Any(x => x == Path.GetFileNameWithoutExtension(dependency)))
            {
                prefix = "costura64.";
                hasUnmanaged = true;
            }

            if (String.IsNullOrEmpty(prefix))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(dependency);
            var resourceName = Embed(prefix, fullPath, config.DisableCompression);
            assembliesAdded = true;

            checksums.Add(resourceName, CalculateChecksum(fullPath));
            if (!config.IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                resourceName = Embed(prefix, pdbFullPath, config.DisableCompression);
                assembliesAdded = true;

                checksums.Add(resourceName, CalculateChecksum(pdbFullPath));
            }
        }

        if (!assembliesAdded)
        {
            LogInfo("No assemblies were embedded");
        }
    }

    private IEnumerable<string> GetFilteredReferences(IEnumerable<string> onlyBinaries, Configuration config)
    {
        if (config.IncludeAssemblies.Any())
        {
            var skippedAssemblies = new List<string>(config.IncludeAssemblies);

            foreach (var file in onlyBinaries)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(file);

                if (config.IncludeAssemblies.Any(x => x == assemblyName) &&
                    config.Unmanaged32Assemblies.All(x => x != assemblyName) &&
                    config.Unmanaged64Assemblies.All(x => x != assemblyName))
                {
                    skippedAssemblies.Remove(assemblyName);
                    yield return file;
                }
            }

            if (skippedAssemblies.Count > 0)
            {
                if (References == null)
                {
                    throw new WeavingException("To embed references with CopyLocal='false', References is required - you may need to update to the latest version of Fody.");
                }

                var splittedReferences = References.Split(';');

                foreach (var skippedAssembly in skippedAssemblies)
                {
                    var fileName = (from splittedReference in splittedReferences
                                    where string.Equals(Path.GetFileNameWithoutExtension(splittedReference), skippedAssembly, StringComparison.InvariantCultureIgnoreCase)
                                    select splittedReference).FirstOrDefault();
                    if (string.IsNullOrEmpty(fileName))
                    {
                        LogError(string.Format("Assembly '{0}' cannot be found (not even as CopyLocal='false'), please update the configuration", skippedAssembly));
                    }

                    yield return fileName;
                }
            }

            yield break;
        }
        if (config.ExcludeAssemblies.Any())
        {
            foreach (var file in onlyBinaries.Except(config.Unmanaged32Assemblies).Except(config.Unmanaged64Assemblies))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(file);

                if (config.ExcludeAssemblies.Any(x => x == assemblyName) ||
                    config.Unmanaged32Assemblies.Any(x => x == assemblyName) ||
                    config.Unmanaged64Assemblies.Any(x => x == assemblyName))
                {
                    continue;
                }
                yield return file;
            }
            yield break;
        }
        if (config.OptOut)
        {
            foreach (var file in onlyBinaries)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(file);

                if (config.Unmanaged32Assemblies.All(x => x != assemblyName) &&
                    config.Unmanaged64Assemblies.All(x => x != assemblyName))
                {
                    yield return file;
                }
            }
        }
    }

    private string Embed(string prefix, string fullPath, bool compress)
    {
        var resourceName = String.Format("{0}{1}", prefix, Path.GetFileName(fullPath).ToLowerInvariant());
        if (ModuleDefinition.Resources.Any(x => x.Name == resourceName))
        {
            LogInfo(string.Format("\tSkipping '{0}' because it is already embedded", fullPath));
            return resourceName;
        }

        if (compress)
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
            using (var cacheFileStream = File.Open(cacheFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                using (var fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (compress)
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
                memoryStream.CopyTo(cacheFileStream);
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