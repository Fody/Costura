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

        var references = GetReferences();
        var embeddedReferences = new List<EmbeddedReferenceInfo>();

        var disableCompression = config.DisableCompression;
        var createTemporaryAssemblies = config.CreateTemporaryAssemblies;

        var normalReferences = GetFilteredReferences(references, config).ToList();
        if (normalReferences.Any())
        {
            WriteInfo("\tIncluding references");

            foreach (var reference in normalReferences)
            {
                var referencePath = reference.FullPath;
                var relativeFileName = reference.RelativeFileName;
                var relativePrefix = reference.GetResourceNamePrefix("costura.");

                if (reference.IsResourcesAssembly && config.IgnoreSatelliteAssemblies)
                {
                    continue;
                }

                var embeddedReference = Embed(relativePrefix, relativeFileName, referencePath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                if (embeddedReference is null == false)
                {
                    embeddedReferences.Add(embeddedReference);
                }

                if (config.IncludeDebugSymbols)
                {
                    var pdbFullPath = Path.ChangeExtension(referencePath, "pdb");
                    if (File.Exists(pdbFullPath))
                    {
                        var embeddedPdb = Embed(relativePrefix, Path.ChangeExtension(relativeFileName, "pdb"), pdbFullPath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                        if (embeddedPdb is null == false)
                        {
                            embeddedReferences.Add(embeddedPdb);
                        }
                    }
                }
            }
        }

        if (config.IncludeRuntimeReferences)
        {
            if (!ModuleDefinition.IsUsingDotNetCore())
            {
                WriteInfo("\tSkipping runtime references for this target framework, library doesn't target .NET Core");
            }
            else
            {
                var runtimeReferences = GetFilteredRuntimeReferences(references, config).ToList();
                if (runtimeReferences.Any())
                {
                    WriteInfo("\tIncluding runtime references");

                    foreach (var runtimeReference in runtimeReferences)
                    {
                        var runtimeReferencePath = runtimeReference.FullPath;
                        var relativeFileName = runtimeReference.RelativeFileName;
                        var relativePrefix = runtimeReference.GetResourceNamePrefix("costura.");

                        if (runtimeReference.IsResourcesAssembly && config.IgnoreSatelliteAssemblies)
                        {
                            continue;
                        }

                        var embeddedReference = Embed(relativePrefix, relativeFileName, runtimeReferencePath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                        if (embeddedReference is null == false)
                        {
                            embeddedReferences.Add(embeddedReference);
                        }

                        if (config.IncludeDebugSymbols)
                        {
                            var pdbFullPath = Path.ChangeExtension(runtimeReferencePath, "pdb");
                            if (File.Exists(pdbFullPath))
                            {
                                var embeddedPdb = Embed(relativePrefix, Path.ChangeExtension(relativeFileName, "pdb"), pdbFullPath, !disableCompression, createTemporaryAssemblies, config.DisableCleanup);
                                if (embeddedPdb is null == false)
                                {
                                    embeddedReferences.Add(embeddedPdb);
                                }
                            }
                        }
                    }
                }
            }
        }

        foreach (var reference in references)
        {
            var prefix = string.Empty;

            if (config.Unmanaged32Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(reference.FullPath), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura32.";
                _hasUnmanaged = true;
            }

            if (config.Unmanaged64Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(reference.FullPath), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura64.";
                _hasUnmanaged = true;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                continue;
            }

            var relativePath = reference.RelativeFileName;
            var fullPath = reference.FullPath;

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

    private List<Reference> GetReferences()
    {
        var references = new List<Reference>();

        foreach (var item in ReferenceCopyLocalPaths)
        {
            if (!item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !item.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reference = new Reference(item);
            references.Add(reference);
        }

        return references;
    }

    private IEnumerable<Reference> GetFilteredReferences(IEnumerable<Reference> references, Configuration config)
    {
        references = references.Where(x => !x.IsRuntimeReference);

        var includeList = config.IncludeAssemblies;
        if (includeList.Any())
        {
            var skippedAssemblies = new List<string>(includeList);

            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (includeList.Any(x => CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged32Assemblies.All(x => !CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged64Assemblies.All(x => !CompareAssemblyName(x, assemblyName)))
                {
                    skippedAssemblies.Remove(includeList.First(x => CompareAssemblyName(x, assemblyName)));
                    yield return reference;

                    // Make sure to embed resources, even if not explicitly included
                    if (!config.IgnoreSatelliteAssemblies)
                    {
                        var resourcesAssemblyName = assemblyName += ".resources";
                        var resourcesAssemblyReferences = (from x in references
                                                           where x.IsResourcesAssembly && CompareAssemblyName(x.FileNameWithoutExtension, resourcesAssemblyName)
                                                           select x).ToList();
                        foreach (var resourcesAssemblyReference in resourcesAssemblyReferences)
                        {
                            yield return resourcesAssemblyReference;
                        }
                    }
                }
            }

            if (skippedAssemblies.Count > 0)
            {
                if (References is null)
                {
                    throw new WeavingException("To embed references with CopyLocal='false', References is required - you may need to update to the latest version of Fody.");
                }

                var hasErrors = false;

                foreach (var skippedAssembly in skippedAssemblies)
                {
                    var skippedReference = (from reference in references
                                            where string.Equals(Path.GetFileNameWithoutExtension(reference.FileName), skippedAssembly, StringComparison.InvariantCulture)
                                            select reference).FirstOrDefault();
                    if (skippedReference is null)
                    {
                        hasErrors = true;
                        WriteError($"Assembly '{skippedAssembly}' cannot be found (not even as CopyLocal='false'), please update the configuration");
                        continue;
                    }

                    yield return skippedReference;
                }

                if (hasErrors)
                {
                    throw new WeavingException("One or more errors occurred, please check the log");
                }
            }

            yield break;
        }

        var excludeList = config.ExcludeAssemblies;
        if (excludeList.Any())
        {
            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (excludeList.Any(x => CompareAssemblyName(x, assemblyName)) ||
                    config.Unmanaged32Assemblies.Any(x => CompareAssemblyName(x, assemblyName)) ||
                    config.Unmanaged64Assemblies.Any(x => CompareAssemblyName(x, assemblyName)))
                {
                    continue;
                }

                yield return reference;
            }

            yield break;
        }

        if (config.OptOutAssemblies)
        {
            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (config.Unmanaged32Assemblies.All(x => !CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged64Assemblies.All(x => !CompareAssemblyName(x, assemblyName)))
                {
                    yield return reference;
                }
            }
        }
    }

    private IEnumerable<Reference> GetFilteredRuntimeReferences(IEnumerable<Reference> references, Configuration config)
    {
        references = references.Where(x => x.IsRuntimeReference);

        var includeList = config.IncludeRuntimeAssemblies;
        if (includeList.Any())
        {
            var skippedAssemblies = new List<string>(includeList);

            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (includeList.Any(x => CompareAssemblyName(x, assemblyName)))
                {
                    skippedAssemblies.Remove(includeList.First(x => CompareAssemblyName(x, assemblyName)));
                    yield return reference;

                    // Make sure to embed resources, even if not explicitly included
                    if (!config.IgnoreSatelliteAssemblies)
                    {
                        var resourcesAssemblyName = assemblyName += ".resources";
                        var resourcesAssemblyReferences = (from x in references
                                                           where x.IsResourcesAssembly && CompareAssemblyName(x.FileNameWithoutExtension, resourcesAssemblyName)
                                                           select x).ToList();
                        foreach (var resourcesAssemblyReference in resourcesAssemblyReferences)
                        {
                            yield return resourcesAssemblyReference;
                        }
                    }
                }
            }

            if (skippedAssemblies.Count > 0)
            {
                if (References is null)
                {
                    throw new WeavingException("To embed references with CopyLocal='false', References is required - you may need to update to the latest version of Fody.");
                }

                var hasErrors = false;

                foreach (var skippedAssembly in skippedAssemblies)
                {
                    var skippedReference = (from reference in references
                                            where string.Equals(Path.GetFileNameWithoutExtension(reference.FileName), skippedAssembly, StringComparison.InvariantCulture)
                                            select reference).FirstOrDefault();
                    if (skippedReference is null)
                    {
                        hasErrors = true;
                        WriteError($"Assembly '{skippedAssembly}' cannot be found (not even as CopyLocal='false'), please update the configuration");
                        continue;
                    }

                    yield return skippedReference;
                }

                if (hasErrors)
                {
                    throw new WeavingException("One or more errors occurred, please check the log");
                }
            }

            yield break;
        }

        var excludeList = config.ExcludeRuntimeAssemblies;
        if (excludeList.Any())
        {
            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (excludeList.Any(x => CompareAssemblyName(x, assemblyName)))
                {
                    continue;
                }

                yield return reference;
            }

            yield break;
        }

        if (config.OptOutRuntimeAssemblies)
        {
            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (config.Unmanaged32Assemblies.All(x => !CompareAssemblyName(x, assemblyName)) &&
                    config.Unmanaged64Assemblies.All(x => !CompareAssemblyName(x, assemblyName)))
                {
                    yield return reference;
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

            WriteDebug($"\t\tSkipping '{fullPath}' because it is already embedded");
            return null;
        }

        if (compress)
        {
            resourceName += ".compressed";

            if (ModuleDefinition.Resources.Any(x => string.Equals(x.Name, resourceName, StringComparison.OrdinalIgnoreCase)))
            {
                // an assembly that appeared twice in the ReferenceCopyLocalPaths, e.g. the same library from different nuget packages (https://github.com/Fody/Costura/issues/332)
                WriteDebug($"\t\tSkipping '{fullPath}' because it is already embedded");
                return null;
            }
        }

        WriteInfo($"\t\tEmbedding '{fullPath}'");

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
