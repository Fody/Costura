﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

static class ILTemplateWithTempAssembly
{
    static readonly Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    static string tempBasePath;

    static readonly List<string> preloadList = new List<string>();
    static readonly List<string> preload32List = new List<string>();
    static readonly List<string> preload64List = new List<string>();

    static readonly Dictionary<string, string> checksums = new Dictionary<string, string>();

    public static void Attach()
    {
        //Create a unique Temp directory for the application path.
        var md5Hash = "To be replaced at compile time";
        var prefixPath = Path.Combine(Path.GetTempPath(), "Costura");
        tempBasePath = Path.Combine(prefixPath, md5Hash);

        // Preload
        var unmanagedAssemblies = IntPtr.Size == 8 ? preload64List : preload32List;
        var libList = new List<string>();
        libList.AddRange(unmanagedAssemblies);
        libList.AddRange(preloadList);
        Common.PreloadUnmanagedLibraries(md5Hash, tempBasePath, libList, checksums);

        var currentDomain = AppDomain.CurrentDomain;
		var reenterant = new Dictionary<string, object>();
		currentDomain.AssemblyResolve += (s, e) =>
		{
			if (reenterant.ContainsKey(e.Name))
				return null;
			try
			{
				reenterant[e.Name] = null;
				return ResolveAssembly(e.Name);
			}
			finally
			{
				reenterant.Remove(e.Name);
			}
		};
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

        assembly = Common.ReadFromDiskCache(tempBasePath, requestedAssemblyName);
        if (assembly == null)
        {
            nullCache.Add(assemblyName, true);

            // Handles retargeted assemblies like PCL
            if (requestedAssemblyName.Flags == AssemblyNameFlags.Retargetable)
            {
                assembly = Assembly.Load(requestedAssemblyName);
            }
        }
        return assembly;
    }
}