﻿using System;
using System.Collections.Generic;
using System.Reflection;

static class ILTemplate
{
    static readonly Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    static readonly Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    static readonly Dictionary<string, string> symbolNames = new Dictionary<string, string>();
    static readonly Dictionary<string, object> assemblyResourceNameCache = new Dictionary<string, object>();

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

        assembly = Common.ReadFromEmbeddedResources(assemblyResourceNameCache, assemblyNames, symbolNames, requestedAssemblyName);
        if (assembly == null)
        {
            nullCache.Add(assemblyName, true);
        }
        return assembly;
    }
}