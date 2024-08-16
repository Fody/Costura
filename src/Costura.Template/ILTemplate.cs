﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

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

        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
    }

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs e)
    {
        lock (nullCacheLock)
        {
            if (nullCache.ContainsKey(e.Name))
            {
                return null;
            }
        }

        var requestedAssemblyName = new AssemblyName(e.Name);

        var assembly = Common.ReadExistingAssembly(requestedAssemblyName);
        if (assembly is not null)
        {
            return assembly;
        }

        Common.Log("Loading assembly '{0}' into the AppDomain", requestedAssemblyName);

        assembly = Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, requestedAssemblyName);
        if (assembly is null)
        {
            lock (nullCacheLock)
            {
                nullCache[e.Name] = true;
            }

            // Handles re-targeted assemblies like PCL
            if ((requestedAssemblyName.Flags & AssemblyNameFlags.Retargetable) != 0)
            {
                assembly = Assembly.Load(requestedAssemblyName);
            }
        }
        return assembly;
    }
}
