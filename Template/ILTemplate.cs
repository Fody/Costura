using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

static class ILTemplate
{
    readonly static Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    readonly static Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    public static void Attach()
    {
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
    }

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name.ToLowerInvariant();

        var existingAssembly = Common.ReadExistingAssembly(name);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        return Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, name);
    }
}
