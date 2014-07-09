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
            // we let Fusion try to load it. We have to do this because "Common.ReadFromEmbeddedResources" uses Assembly.LoadFile
            // that doesn't automatically load the dependencies.
            // From: http://blogs.msdn.com/b/suzcook/archive/2003/09/19/loadfile-vs-loadfrom.aspx
            //
            // " LoadFile() has a catch. Since it doesn't use a binding context, its dependencies aren't automatically 
            // " found in its directory. If they aren't available in the Load context, you would have to subscribe to the 
            // " AssemblyResolve event in order to bind to them."
            //
            // If the dependencies is not found we won't get any StackOverflowException (caused by the call to Assembly.Load
            // that recursively fire another AssemblyResolve event) because we have already added the requestedAssemblyName
            // to the nullCache and we'll just skip it next time it's "asked".
            assembly = Assembly.Load(requestedAssemblyName);
        }
        return assembly;
    }
}