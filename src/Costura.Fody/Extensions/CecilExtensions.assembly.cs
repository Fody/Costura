using System;
using Mono.Cecil;

public static partial class CecilExtensions
{
    public static Version GetVersion(this AssemblyDefinition assemblyDefinition)
    {
        return assemblyDefinition.Name.Version;

        //var stringVersion = "0.0.0.0";

        //var assemblyVersionAttributeName = typeof(AssemblyVersionAttribute).FullName;
        //var assemblyFileVersionAttributeName = typeof(AssemblyFileVersionAttribute).FullName;

        //var attribute = assemblyDefinition.CustomAttributes.FirstOrDefault(_ => _.AttributeType.FullName == assemblyVersionAttributeName);
        //if (attribute is null)
        //{
        //    attribute = assemblyDefinition.CustomAttributes.FirstOrDefault(_ => _.AttributeType.FullName == assemblyFileVersionAttributeName);
        //}

        //if (attribute is not null)
        //{
        //    stringVersion = (string)attribute.ConstructorArguments.First().Value;
        //}

        //var version = new Version(stringVersion);
        //return version;
    }

    public static bool IsNetStandardLibrary(this AssemblyDefinition assemblyDefinition)
    {
        return assemblyDefinition.MainModule.FileName.IndexOf("netstandard", 0, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsUsingDotNetCore(this AssemblyDefinition assemblyDefinition)
    {
        return assemblyDefinition.MainModule.IsUsingDotNetCore();
    }

    public static bool IsUsingDotNetCore(this ModuleDefinition moduleDefinition)
    {
        using (var resolvedAssembly = moduleDefinition.AssemblyResolver.Resolve("System.Runtime.Loader"))
        {
            return resolvedAssembly is not null;
        }
    }

    public static AssemblyDefinition Resolve(this IAssemblyResolver assemblyResolver, string name)
    {
        return assemblyResolver.Resolve(new AssemblyNameReference(name, null));
    }
}
