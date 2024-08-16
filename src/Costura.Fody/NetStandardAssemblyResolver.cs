using System;
using System.Collections.Generic;
using Mono.Cecil;

/// <summary>
/// An assembly resolver that resolves with the <see cref="IAssemblyResolver"/> of the <see cref="ModuleDefinition"/>
/// of the given <see cref="ModuleWeaver"/> with special handling for the <c>netstandard</c> assembly reference to
/// support .NET Framework 4.7 and lower.
/// </summary>
public sealed class NetStandardAssemblyResolver : IAssemblyResolver
{
    private readonly ModuleWeaver _weaver;
    private readonly HashSet<AssemblyNameReference> _resolvedReferences;
    private readonly Lazy<AssemblyDefinition> _netStandardAssemblyDefinition;

    public NetStandardAssemblyResolver(ModuleWeaver weaver)
    {
        _weaver = weaver ?? throw new ArgumentNullException(nameof(weaver));
        _weaver.WriteDebug("\tResolving assembly references");
        _resolvedReferences = new HashSet<AssemblyNameReference>();
        _netStandardAssemblyDefinition = new Lazy<AssemblyDefinition>(() =>
        {
            const string dllName = "Costura.NETFramework.netstandard.dll";
            var assembly = GetType().Assembly;
            using (var stream = assembly.GetManifestResourceStream(dllName))
            {
                if (stream is null)
                {
                    throw new InvalidOperationException($"Failed to get the manifest resource stream named '{dllName}' on {assembly}");
                }

                return AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { AssemblyResolver = this });
            }
        });
    }

    public void Dispose()
    {
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        var assemblyDefinition = _weaver.ModuleDefinition.AssemblyResolver.Resolve(name);
        return Resolve(name, assemblyDefinition);
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        var assemblyDefinition = _weaver.ModuleDefinition.AssemblyResolver.Resolve(name, parameters);
        return Resolve(name, assemblyDefinition);
    }

    private AssemblyDefinition Resolve(AssemblyNameReference name, AssemblyDefinition assemblyDefinition)
    {
        if (assemblyDefinition is not null)
        {
            return ResolvedReference(name, assemblyDefinition);
        }

        if (name.Name == "netstandard" && !_weaver.ModuleDefinition.IsUsingDotNetCore())
        {
            var netStandardAssemblyDefinition = _netStandardAssemblyDefinition.Value;
            return ResolvedReference(name, netStandardAssemblyDefinition);
        }

        throw new AssemblyResolutionException(name);
    }

    private AssemblyDefinition ResolvedReference(AssemblyNameReference name, AssemblyDefinition assemblyDefinition)
    {
        var added = _resolvedReferences.Add(name);
        if (added)
        {
            var toFileName = string.IsNullOrEmpty(assemblyDefinition.MainModule.FileName) ? string.Empty : $" to {assemblyDefinition.MainModule.FileName}";
            _weaver.WriteDebug($"\t\tResolved {name}{toFileName}");
        }

        return assemblyDefinition ?? throw new AssemblyResolutionException(name);
    }
}
