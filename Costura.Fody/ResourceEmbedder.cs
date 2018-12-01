﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

using Mono.Cecil;

partial class ModuleWeaver : IDisposable
{
    List<Stream> streams = new List<Stream>();
    string cachePath;

    void EmbedResources(Configuration config)
    {
        if (ReferenceCopyLocalPaths == null)
        {
            throw new WeavingException("ReferenceCopyLocalPaths is required you may need to update to the latest version of Fody.");
        }

        cachePath = Path.Combine(Path.GetDirectoryName(AssemblyFilePath), "Costura");
        Directory.CreateDirectory(cachePath);

        var binaryReferences = ReferenceCopyLocalPaths.Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        
        var onlyBinaries = Distinct(binaryReferences).ToArray();

        foreach (var dependency in GetFilteredReferences(onlyBinaries, config))
        {
            var fullPath = Path.GetFullPath(dependency);

            string resourceName;

            if (!config.IgnoreSatelliteAssemblies)
            {
                if (dependency.EndsWith(".resources.dll",StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = Embed($"costura.{Path.GetFileName(Path.GetDirectoryName(fullPath))}.", fullPath, !config.DisableCompression);
                    if (config.CreateTemporaryAssemblies)
                    {
                        checksums.Add(resourceName, CalculateChecksum(fullPath));
                    }
                    continue;
                }
            }

            resourceName = Embed("costura.", fullPath, !config.DisableCompression);
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
                if (config.CreateTemporaryAssemblies)
                {
                    checksums.Add(resourceName, CalculateChecksum(pdbFullPath));
                }
            }
        }

        foreach (var dependency in onlyBinaries)
        {
            var prefix = "";

            if (config.Unmanaged32Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(dependency), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura32.";
                hasUnmanaged = true;
            }
            if (config.Unmanaged64Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(dependency), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura64.";
                hasUnmanaged = true;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(dependency);
            var resourceName = Embed(prefix, fullPath, !config.DisableCompression);
            checksums.Add(resourceName, CalculateChecksum(fullPath));
            if (!config.IncludeDebugSymbols)
            {
                continue;
            }
            var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
            if (File.Exists(pdbFullPath))
            {
                resourceName = Embed(prefix, pdbFullPath, !config.DisableCompression);
                checksums.Add(resourceName, CalculateChecksum(pdbFullPath));
            }
        }
    }

    IEnumerable<string> GetFilteredReferences(IEnumerable<string> onlyBinaries, Configuration config)
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
                        LogError($"Assembly '{skippedAssembly}' cannot be found (not even as CopyLocal='false'), please update the configuration");
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

    string Embed(string prefix, string fullPath, bool compress)
    {
        var resourceName = $"{prefix}{Path.GetFileName(fullPath).ToLowerInvariant()}";

        if (compress)
        {
            resourceName += ".compressed";
        }

        if (ModuleDefinition.Resources.Any(x => string.Equals(x.Name, resourceName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new WeavingException($"'{fullPath}' is already embedded.");
        }

        LogInfo($"\tEmbedding '{fullPath}'");

        var checksum = CalculateChecksum(fullPath);
        var cacheFile = Path.Combine(cachePath, $"{checksum}.{resourceName}");
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

        ReferenceCopyLocalPaths.RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));

        return resourceName;
    }

    IEnumerable<string> Distinct(IEnumerable<string> assemblyFiles)
    {
        var assembliesByName = assemblyFiles
            .Select(file => AssemblyName.GetAssemblyName(file))
            .GroupBy(a => a.Name);

        foreach (var group in assembliesByName)
        {
            if (group.Count() == 1)
            {
                yield return group.Single().LocalPath();
            }
            else
            {
                var target = group.OrderByDescending(a => a.Version).FirstOrDefault();

                var duplicates = new HashSet<string>(group.Where(a => a != target).Select(a => a.LocalPath()), StringComparer.OrdinalIgnoreCase);

                yield return target.LocalPath();

                ReferenceCopyLocalPaths.RemoveAll(item => duplicates.Contains(item));
            }
        }
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