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
        currentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);
    }

    public static Assembly ResolveAssembly(string assemblyName)
    {
        if (nullCache.ContainsKey(assemblyName))
        {
            return null;
        }

        var requestedAssemblyName = new AssemblyName(assemblyName);

        var assembly = Common.ReadExistingAssembly(requestedAssemblyName);
        if (assembly != null)
        {
            return assembly;
        }

        Common.Log("Loading assembly '{0}' into the AppDomain", requestedAssemblyName);

        assembly = Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, requestedAssemblyName);
        if (assembly == null)
        {
            nullCache.Add(assemblyName, true);

            // We should get here only when we need to load a dependency of a dll we have embedded. If such dependency
            // it's not already loaded ("Common.ReadExistingAssembly" fails), and it's not in the same path of our application
            // we let Fusion try to load it. We have to do this because "Common.ReadFromEmbeddedResources" uses Assembly.Load(byte[])
            // that load the assembly in the "neither context".
            // From: http://blogs.msdn.com/b/suzcook/archive/2003/05/29/choosing-a-binding-context.aspx
            //
            // "If the user generated or found the assembly instead of Fusion, it's in neither context. 
            // This applies to assemblies loaded by Assembly.Load(byte[]) and Reflection Emit assemblies (
            // that haven't been loaded from disk). Assembly.LoadFile() assemblies are also generally loaded into 
            // this context, even though a path is given (because it doesn't go through Fusion)."
            //
            // If Fusion can't find the dependencies of the just loaded assembly in the GAC (for example a retargetable library 
            // like System.Core 2.0.0.5 used by PCL libraries) another AssemblyResolv event will be fired and we'll get here.
            // If the dependencies is not found we won't get any StackOverflowException (caused by the call to Assembly.Load
            // that recursively fire another AssemblyResolve event because we have already added the requestedAssemblyName
            // to the nullCache and we'll just skip it next time it's "asked".
            if (requestedAssemblyName.Flags == AssemblyNameFlags.Retargetable)
            {
                assembly = Assembly.Load(requestedAssemblyName);
            }
        }
        return assembly;
    }
}