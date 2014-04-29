using System;
using System.Collections.Generic;
using System.Reflection;

static class ILTemplate
{
    static readonly Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    static readonly Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    static readonly Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    public static void Attach()
    {
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
    }

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        if (nullCache.ContainsKey(args.Name))
        {
            return null;
        }

        var requestedAssemblyName = new AssemblyName(args.Name);

        var assembly = Common.ReadExistingAssembly(requestedAssemblyName);
        if (assembly != null)
        {
            return assembly;
        }

        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !String.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
            name = String.Format("{0}.{1}", requestedAssemblyName.CultureInfo.Name, name);

        assembly = Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, name);
        if (assembly == null)
        {
            nullCache.Add(args.Name, true);
        }
        return assembly;
    }
}