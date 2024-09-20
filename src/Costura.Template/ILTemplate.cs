using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

#if NETCORE
using System.Runtime.Loader;
#endif

internal static class ILTemplate
{
    private static object nullCacheLock = new object();
    private static Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    private static Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    private static Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    private static int isAttached;

    public static void Attach()
    {
        if (Interlocked.Exchange(ref isAttached, 1) == 1)
        {
            return;
        }

#if NETCORE
        AssemblyLoadContext.Default.Resolving += ResolveAssembly;
#else
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
#endif
    }

#if NETCORE
    public static Assembly ResolveAssembly(AssemblyLoadContext assemblyLoadContext, AssemblyName assemblyName)
#else
    public static Assembly ResolveAssembly(object sender, ResolveEventArgs e)
#endif
    {
#if NETCORE
        var assemblyNameAsString = assemblyName.Name;
#else
        var assemblyNameAsString = e.Name;
        var assemblyName = new AssemblyName(assemblyNameAsString);
#endif

        lock (nullCacheLock)
        {
            if (nullCache.ContainsKey(assemblyNameAsString))
            {
                return null;
            }
        }

        var assembly = Common.ReadExistingAssembly(assemblyName);
        if (assembly is not null)
        {
            return assembly;
        }

        Common.Log("Loading assembly '{0}' into the current context", assemblyName);

        assembly = Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, assemblyName);
        if (assembly is null)
        {
            lock (nullCacheLock)
            {
                nullCache[assemblyNameAsString] = true;
            }

            // Handles re-targeted assemblies like PCL
            if ((assemblyName.Flags & AssemblyNameFlags.Retargetable) != 0)
            {
                assembly = Assembly.Load(assemblyName);
            }
        }

        return assembly;
    }
}
