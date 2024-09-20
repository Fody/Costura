using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Fody;
using Mono.Cecil;

public partial class ModuleWeaver : IDisposable
{
    private readonly List<Stream> _streams = new List<Stream>();
    private string _cachePath;

    private int _fallbackCounter = 1;

    private void EmbedResources(Configuration config)
    {
        if (ReferenceCopyLocalPaths is null)
        {
            throw new WeavingException("ReferenceCopyLocalPaths is required you may need to update to the latest version of Fody.");
        }

        var assemblyDirectory = Path.GetDirectoryName(AssemblyFilePath);
        _cachePath = Path.Combine(assemblyDirectory, "Costura");
        Directory.CreateDirectory(_cachePath);

        var useRuntimeReferencePaths = config.UseRuntimeReferencePaths ?? ModuleDefinition.IsUsingDotNetCore();

        var references = GetReferences(useRuntimeReferencePaths);
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

        foreach (var reference in references)
        {
            var prefix = string.Empty;

            if (config.UnmanagedWinX86Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(reference.FullPath), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura_win_x86.";
                _hasUnmanaged = true;
            }

            if (config.UnmanagedWinX64Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(reference.FullPath), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura_win_x64.";
                _hasUnmanaged = true;
            }

            if (config.UnmanagedWinArm64Assemblies.Any(x => string.Equals(x, Path.GetFileNameWithoutExtension(reference.FullPath), StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "costura_win_arm64.";
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
#pragma warning disable IDISP001 // Dispose created
        var textWriter = new StreamWriter(memoryStream);
#pragma warning restore IDISP001 // Dispose created
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

    private List<Reference> GetReferences(bool useRuntimeReferencePaths)
    {
        var references = new List<Reference>();

        foreach (var item in ReferenceCopyLocalPaths)
        {
            if (!item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !item.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reference = new Reference(item, useRuntimeReferencePaths)
            {
                IsCopyLocal = true
            };

            references.Add(reference);
        }

        if (References is null)
        {
            throw new WeavingException("To embed references with CopyLocal='false', References is required - you may need to update to the latest version of Fody.");
        }

        // Add all references, but mark them as special
        var splittedReferences = References.Split(';').Where(item => !string.IsNullOrEmpty(item));

        foreach (var splittedReference in splittedReferences)
        {
            var fileName = splittedReference;

            if (!Path.IsPathRooted(fileName))
            {
                fileName = Path.GetFullPath(fileName);
            }

            if (!File.Exists(fileName))
            {
                continue;
            }

            if (references.Any(_ => _.FullPath == fileName))
            {
                continue;
            }

            var reference = new Reference(fileName, useRuntimeReferencePaths)
            {
                IsCopyLocal = false
            };

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
                    !IsUnmanagedAssemblyReference(reference, config))
                {
                    skippedAssemblies.Remove(includeList.First(x => CompareAssemblyName(x, assemblyName)));
                    yield return reference;

                    // Make sure to embed resources, even if not explicitly included
                    if (!config.IgnoreSatelliteAssemblies)
                    {
                        var resourcesAssemblyName = assemblyName + ".resources";
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
                foreach (var skippedAssembly in skippedAssemblies)
                {
                    WriteError($"Assembly '{skippedAssembly}' cannot be found (not even as CopyLocal='false'), please update the configuration");
                }

                throw new WeavingException("One or more errors occurred, please check the log");
            }

            yield break;
        }

        // From this point, we want to exclude "CopyLocal=false"
        references = references.Where(_ => _.IsCopyLocal);

        var excludeList = config.ExcludeAssemblies;
        if (excludeList.Any())
        {
            foreach (var reference in references)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

                if (excludeList.Any(x => CompareAssemblyName(x, assemblyName)) ||
                    IsUnmanagedAssemblyReference(reference, config))
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

                if (!IsUnmanagedAssemblyReference(reference, config))
                {
                    yield return reference;
                }
            }
        }
    }

    private bool IsUnmanagedAssemblyReference(Reference reference, Configuration config)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(reference.FileName);

        var listsToCheck = new[]
        {
            config.UnmanagedWinX86Assemblies,
            config.UnmanagedWinX64Assemblies,
            config.UnmanagedWinArm64Assemblies
        };

        foreach (var listToCheck in listsToCheck)
        {
            if (listToCheck.Any(x => CompareAssemblyName(x, assemblyName)))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<Reference> GetFilteredRuntimeReferences(IEnumerable<Reference> references, Configuration config)
    {
        references = references.Where(_ => _.IsRuntimeReference);

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
                foreach (var skippedAssembly in skippedAssemblies)
                {
                    WriteError($"Assembly '{skippedAssembly}' cannot be found (not even as CopyLocal='false'), please update the configuration");
                }

                throw new WeavingException("One or more errors occurred, please check the log");
            }

            yield break;
        }

        // From this point, we want to exclude "CopyLocal=false"
        references = references.Where(_ => _.IsCopyLocal);

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

                if (IsUnmanagedAssemblyReference(reference, config))
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
                _checksums.Add(resourceName, CalculateSha1Checksum(fullPath));
            }

            WriteDebug($"\t\tSkipping '{fullPath}' because it is already embedded");
            return null;
        }

        if (compress)
        {
            resourceName += ".compressed";
        }

        if (ModuleDefinition.Resources.Any(x => string.Equals(x.Name, resourceName, StringComparison.OrdinalIgnoreCase)))
        {
            // an assembly that appeared twice in the ReferenceCopyLocalPaths, e.g. the same library from different nuget packages (https://github.com/Fody/Costura/issues/332)
            WriteDebug($"\t\tSkipping '{fullPath}' because it is already embedded");
            return null;
        }

        WriteInfo($"\t\tEmbedding '{fullPath}'");

        var sha1Checksum = CalculateSha1Checksum(fullPath);
        var cacheFile = GetCacheFile(_cachePath, resourceName, compress, sha1Checksum);

        var memoryStream = BuildMemoryStream(fullPath, compress, cacheFile);
        _streams.Add(memoryStream);
        var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, memoryStream);
        ModuleDefinition.Resources.Add(resource);

        if (addChecksum)
        {
            _checksums.Add(resourceName, sha1Checksum);
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
            Sha1Checksum = sha1Checksum,
            Size = new FileInfo(fullPath).Length
        };
    }

    public string GetCacheFile(string cacheRoot, string resourceName, bool isCompressed, string sha1Checksum, bool allowNumberReplacement = true)
    {
        var cacheFile = Path.Combine(cacheRoot, $"{sha1Checksum}.{resourceName}");

        if (isCompressed)
        {
            cacheFile += ".compressed";
        }

        if (cacheFile.Length > 255)
        {
            // We should never embed more than 10k assemblies at the same time, but we don't want to have a too high number either in case this is a long-living msbuild instance
            if (_fallbackCounter > 10000)
            {
                _fallbackCounter = 1;
            }

            if (!allowNumberReplacement)
            {
                WriteError($"\t\t\tPath length is too large ({cacheFile.Length} characters)");
                return cacheFile;
            }

            var oldCacheFile = cacheFile;
            cacheFile = GetCacheFile(cacheRoot, _fallbackCounter++.ToString(), isCompressed, sha1Checksum, false);

            WriteInfo($"\t\t\tCache file path has too many characters, using simplified random guid as fallback ('{oldCacheFile}' => '{cacheFile}')");

            return cacheFile;
        }

        return cacheFile;
    }

    private MemoryStream BuildMemoryStream(string fullPath, bool compress, string cacheFile)
    {
        var memoryStream = new MemoryStream();

        if (File.Exists(cacheFile))
        {
            WriteInfo($"\t\t\tReusing cached file at '{cacheFile}'");

            using (var fileStream = File.Open(cacheFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fileStream.CopyTo(memoryStream);
                memoryStream.Flush();
            }
        }
        else
        {
            WriteInfo($"\t\t\tCreating cached file at '{cacheFile}'");

            var stopwatch = Stopwatch.StartNew();

            WriteInfo($"\t\t\tCreating target file");

            using (var cacheFileStream = File.Open(cacheFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                WriteInfo($"\t\t\tOpening source file");

                using (var fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (compress)
                    {
                        WriteInfo($"\t\t\tCompressing file");

                        using (var compressedStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
                        {
                            fileStream.CopyTo(compressedStream);
                            compressedStream.Flush();
                        }
                    }
                    else
                    {
                        fileStream.CopyTo(memoryStream);
                    }

                    memoryStream.Flush();
                }

                memoryStream.Position = 0L;
                memoryStream.CopyTo(cacheFileStream);
                cacheFileStream.Flush();
            }

            WriteInfo($"\t\t\tCreated cached file at '{cacheFile}', took '{stopwatch.ElapsedMilliseconds}' ms");
        }

        memoryStream.Position = 0L;
        return memoryStream;
    }

    public void Dispose()
    {
        if (_streams is null)
        {
            return;
        }
        foreach (var stream in _streams)
        {
            stream.Dispose();
        }
    }
}
