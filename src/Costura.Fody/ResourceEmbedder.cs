using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Fody;
using Mono.Cecil;

public partial class ModuleWeaver : IDisposable
{
    private readonly List<Stream> _streams = new List<Stream>();
    private string _cachePath;

    private void EmbedResources(Configuration config)
    {
        if (ReferenceCopyLocalPaths is null)
        {
            throw new WeavingException("ReferenceCopyLocalPaths is required you may need to update to the latest version of Fody.");
        }

        var assemblyDirectory = Path.GetDirectoryName(AssemblyFilePath);
        _cachePath = Path.Combine(assemblyDirectory, "Costura");
        Directory.CreateDirectory(_cachePath);

        var embeddedReferences = new List<EmbeddedReferenceInfo>();

        var onlyBinaries = ReferenceCopyLocalPaths.Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).ToArray();

        var disableCompression = config.DisableCompression;
        var createTemporaryAssemblies = config.CreateTemporaryAssemblies;

        foreach (var dependency in GetFilteredReferences(onlyBinaries, config))
        {
            var relativePath = Path.GetFileName(dependency);
            var fullPath = Path.GetFullPath(dependency);

            if (!config.IgnoreSatelliteAssemblies)
            {
                if (dependency.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                {
                    var embeddedResourceReference = Embed($"costura.{Path.GetFileName(Path.GetDirectoryName(fullPath))}.", relativePath, fullPath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                    if (embeddedResourceReference is null == false)
                    {
                        embeddedReferences.Add(embeddedResourceReference);
                    }
                    continue;
                }
            }

            var embeddedReference = Embed("costura.", relativePath, fullPath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
            if (embeddedReference is null == false)
            {
                embeddedReferences.Add(embeddedReference);
            }

            if (config.IncludeDebugSymbols)
            {
                var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
                if (File.Exists(pdbFullPath))
                {
                    var embeddedPdb = Embed("costura.", Path.ChangeExtension(relativePath, "pdb"), pdbFullPath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                    if (embeddedPdb is null == false)
                    {
                        embeddedReferences.Add(embeddedPdb);
                    }
                }
            }
        }

        if (config.IncludeRuntimeReferences)
        {
            const string RuntimesFolderName = "runtimes";
            var runtimesDirectory = Path.Combine(assemblyDirectory, RuntimesFolderName);

            // For now just support dll files
            foreach (var runtimeReferencePath in Directory.GetFiles(runtimesDirectory, "*.dll", SearchOption.AllDirectories))
            {
                // Get relative prefix
                var relativePrefix = string.Empty;
                var relativeFileName = Path.GetFileName(runtimeReferencePath);

                var parentDirectory = Path.GetDirectoryName(runtimeReferencePath);
                var directoryName = Path.GetFileName(parentDirectory);

                while (!directoryName.Equals(RuntimesFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    relativePrefix = $"{directoryName}.{relativePrefix}";
                    relativeFileName = $"{directoryName}/{relativeFileName}";

                    parentDirectory = Path.GetDirectoryName(parentDirectory);
                    directoryName = Path.GetFileName(parentDirectory);
                }

                relativePrefix = $"{RuntimesFolderName}.{relativePrefix}";
                relativeFileName = $"{RuntimesFolderName}/{relativeFileName}";

                if (!config.IgnoreSatelliteAssemblies)
                {
                    if (runtimeReferencePath.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        var embeddedResourceReference = Embed($"costura.{relativePrefix}.{Path.GetFileName(Path.GetDirectoryName(runtimeReferencePath))}.", relativeFileName, runtimeReferencePath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                        if (embeddedResourceReference is null == false)
                        {
                            embeddedReferences.Add(embeddedResourceReference);
                        }
                        continue;
                    }
                }

                var embeddedReference = Embed($"costura.{relativePrefix}", relativeFileName, runtimeReferencePath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                if (embeddedReference is null == false)
                {
                    embeddedReferences.Add(embeddedReference);
                }

                if (config.IncludeDebugSymbols)
                {
                    var pdbFullPath = Path.ChangeExtension(runtimeReferencePath, "pdb");
                    if (File.Exists(pdbFullPath))
                    {
                        var embeddedPdb = Embed($"costura.{relativePrefix}", Path.ChangeExtension(relativeFileName, "pdb"), pdbFullPath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                        if (embeddedPdb is null == false)
                        {
                            embeddedReferences.Add(embeddedPdb);
                        }
                    }
                }
            }
        }

        foreach (var dependency in onlyBinaries)
        {
            var prefix = string.Empty;

            if (config.Unmanaged32Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(dependency), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura32.";
                _hasUnmanaged = true;
            }

            if (config.Unmanaged64Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(dependency), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura64.";
                _hasUnmanaged = true;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                continue;
            }

            var relativePath = Path.GetFileName(dependency);
            var fullPath = Path.GetFullPath(dependency);

            var embeddedReference = Embed(prefix, relativePath, fullPath, !disableCompression, true, config.DisableCleanup);
            if (embeddedReference is null == false)
            {
                embeddedReferences.Add(embeddedReference);
            }

            if (config.IncludeDebugSymbols)
            {
                var pdbFullPath = Path.ChangeExtension(fullPath, "pdb");
                if (File.Exists(pdbFullPath))
                {
                    var embeddedPdb = Embed(prefix, relativePath, pdbFullPath, !disableCompression, true, config.DisableCleanup);
                    if (embeddedPdb is null == false)
                    {
                        embeddedReferences.Add(embeddedPdb);
                    }
                }
            }
        }

        EmbedMetadata(embeddedReferences);
    }

    private void EmbedMetadata(List<EmbeddedReferenceInfo> references)
    {
        // Write metadata file
        var stringBuilder = new StringBuilder();
        references.ForEach(x => stringBuilder.AppendLine(x.ToString()));

        var metadata = stringBuilder.ToString();

        var memoryStream = new MemoryStream();
        var textWriter = new StreamWriter(memoryStream);
        textWriter.Write(metadata);
        textWriter.Flush();

        _streams.Add(memoryStream);

        var resource = new EmbeddedResource("costura.metadata", ManifestResourceAttributes.Private, memoryStream);
        ModuleDefinition.Resources.Add(resource);
    }

    private bool CompareAssemblyName(string matchText, string assemblyName)
    {
        if (matchText.EndsWith("*") && matchText.Length > 1)
        {
            return assemblyName.StartsWith(matchText.Substring(0, matchText.Length - 1), StringComparison.OrdinalIgnoreCase);
        }

        return matchText.Equals(assemblyName, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> GetFilteredReferences(IEnumerable<string> onlyBinaries, Configuration config)
    {
        if (config.IncludeAssemblies.Any())
        {
            var skippedAssemblies = new List<string>(config.IncludeAssemblies);

            foreach (var file in onlyBinaries)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(file);

                if (config.IncludeAssemblies.Any(x => CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged32Assemblies.All(x => !CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged64Assemblies.All(x => !CompareAssemblyName(x, assemblyName)))
                {
                    skippedAssemblies.Remove(config.IncludeAssemblies.First(x => CompareAssemblyName(x, assemblyName)));
                    yield return file;
                }
            }

            if (skippedAssemblies.Count > 0)
            {
                if (References is null)
                {
                    throw new WeavingException("To embed references with CopyLocal='false', References is required - you may need to update to the latest version of Fody.");
                }

                var splittedReferences = References.Split(';');

                var hasErrors = false;

                foreach (var skippedAssembly in skippedAssemblies)
                {
                    var fileName = (from splittedReference in splittedReferences
                                    where string.Equals(Path.GetFileNameWithoutExtension(splittedReference), skippedAssembly, StringComparison.InvariantCulture)
                                    select splittedReference).FirstOrDefault();
                    if (string.IsNullOrEmpty(fileName))
                    {
                        hasErrors = true;
                        WriteError($"Assembly '{skippedAssembly}' cannot be found (not even as CopyLocal='false'), please update the configuration");
                        continue;
                    }

                    yield return fileName;
                }

                if (hasErrors)
                {
                    throw new WeavingException("One or more errors occurred, please check the log");
                }
            }

            yield break;
        }
        if (config.ExcludeAssemblies.Any())
        {
            foreach (var file in onlyBinaries.Except(config.Unmanaged32Assemblies).Except(config.Unmanaged64Assemblies))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(file);

                if (config.ExcludeAssemblies.Any(x => CompareAssemblyName(x, assemblyName)) ||
                    config.Unmanaged32Assemblies.Any(x => CompareAssemblyName(x, assemblyName)) ||
                    config.Unmanaged64Assemblies.Any(x => CompareAssemblyName(x, assemblyName)))
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

                if (config.Unmanaged32Assemblies.All(x => !CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged64Assemblies.All(x => !CompareAssemblyName(x, assemblyName)))
                {
                    yield return file;
                }
            }
        }
    }

    private EmbeddedReferenceInfo Embed(string prefix, string relativePath, string fullPath, bool compress, bool addChecksum, bool disableCleanup)
    {
        try
        {
            return InnerEmbed(prefix, relativePath, fullPath, compress, addChecksum, disableCleanup);
        }
        catch (Exception exception)
        {
            throw new Exception(
                innerException: exception,
                message: $@"Failed to embed.
prefix: {prefix}
fullPath: {fullPath}
compress: {compress}
addChecksum: {addChecksum}
disableCleanup: {disableCleanup}");
        }
    }

    private EmbeddedReferenceInfo InnerEmbed(string prefix, string relativePath, string fullPath, bool compress, bool addChecksum, bool disableCleanup)
    {
        if (!disableCleanup)
        {
            // in any case we can remove this from the copy local paths, because either it's already embedded, or it will be embedded.
            ReferenceCopyLocalPaths.RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        var resourceName = $"{prefix}{Path.GetFileName(fullPath).ToLowerInvariant()}";

        if (ModuleDefinition.Resources.Any(x => string.Equals(x.Name, resourceName, StringComparison.OrdinalIgnoreCase)))
        {
            // - an assembly that is already embedded uncompressed, using <EmbeddedResource> in the project file
            // - if compress == false: an assembly that appeared twice in the ReferenceCopyLocalPaths, e.g. the same library from different nuget packages (https://github.com/Fody/Costura/issues/332)
            if (addChecksum && !_checksums.ContainsKey(resourceName))
            {
                _checksums.Add(resourceName, CalculateChecksum(fullPath));
            }

            WriteDebug($"\tSkipping '{fullPath}' because it is already embedded");
            return null;
        }

        if (compress)
        {
            resourceName += ".compressed";

            if (ModuleDefinition.Resources.Any(x => string.Equals(x.Name, resourceName, StringComparison.OrdinalIgnoreCase)))
            {
                // an assembly that appeared twice in the ReferenceCopyLocalPaths, e.g. the same library from different nuget packages (https://github.com/Fody/Costura/issues/332)
                WriteDebug($"\tSkipping '{fullPath}' because it is already embedded");
                return null;
            }
        }

        WriteDebug($"\tEmbedding '{fullPath}'");

        var checksum = CalculateChecksum(fullPath);
        var cacheFile = Path.Combine(_cachePath, $"{checksum}.{resourceName}");
        var memoryStream = BuildMemoryStream(fullPath, compress, cacheFile);
        _streams.Add(memoryStream);
        var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, memoryStream);
        ModuleDefinition.Resources.Add(resource);

        if (addChecksum)
        {
            _checksums.Add(resourceName, checksum);
        }

        var version = string.Empty;
        AssemblyName assemblyName = null;

        if (relativePath.ToLower().EndsWith(".dll"))
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(fullPath);
                version = versionInfo.FileVersion;

                assemblyName = AssemblyName.GetAssemblyName(fullPath);
            }
            catch (Exception)
            {
                // Native assemblies don't have assembly names
            }
        }

        return new EmbeddedReferenceInfo
        {
            ResourceName = resourceName,
            Version = assemblyName?.Version.ToString(4) ?? version,
            AssemblyName = assemblyName?.FullName ?? string.Empty,
            RelativeFileName = relativePath,
            Checksum = checksum,
        };
    }

    private static MemoryStream BuildMemoryStream(string fullPath, bool compress, string cacheFile)
    {
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
        return memoryStream;
    }

    public void Dispose()
    {
        if (_streams == null)
        {
            return;
        }
        foreach (var stream in _streams)
        {
            stream.Dispose();
        }
    }
}
