using System;
using System.Collections.Generic;
using System.Reflection;

static class ILTemplate
{
    readonly static Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    readonly static Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    static AssemblyName[] referencedAssemblies;

    public static void Attach()
    {
        referencedAssemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
    }

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        var requestedAssemblyName = new AssemblyName(args.Name);

        foreach (var assembly in referencedAssemblies)
        {
            if (assembly.Name == requestedAssemblyName.Name && assembly.Version != requestedAssemblyName.Version)
            {
                return Assembly.Load(assembly);
            }
        }

        var existingAssembly = Common.ReadExistingAssembly(requestedAssemblyName);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !String.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
            name = String.Format("{0}.{1}", requestedAssemblyName.CultureInfo.Name, name);

        return Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, name);
    }
}