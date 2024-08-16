﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;

internal static class ILTemplateWithTempAssembly
{
    private static object nullCacheLock = new object();
    private static Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    private static string tempBasePath;

    private static List<string> preloadList = new List<string>();
    private static List<string> preload32List = new List<string>();
    private static List<string> preload64List = new List<string>();

    private static Dictionary<string, string> checksums = new Dictionary<string, string>();

    private static int isAttached;

    public static void Attach()
    {
        if (Interlocked.Exchange(ref isAttached, 1) == 1)
        {
            return;
        }

        var currentDomain = AppDomain.CurrentDomain;

        // Make sure the target framework is set in order not to interfere with AppContext switches initialization
        // See https://github.com/Fody/Costura/issues/633 for full explanation
        var setupInformation = currentDomain.GetType()?.GetProperty("SetupInformation")?.GetValue(currentDomain);
        var targetFrameworkNameProperty = setupInformation?.GetType()?.GetProperty("TargetFrameworkName");
        if (targetFrameworkNameProperty is not null && targetFrameworkNameProperty.GetValue(setupInformation) is null)
        {
            var targetFrameworkAttribute = (TargetFrameworkAttribute)Assembly.GetCallingAssembly()?.GetCustomAttribute(typeof(TargetFrameworkAttribute));
            var targetFrameworkName = targetFrameworkAttribute?.FrameworkName;
            if (targetFrameworkName is not null)
            {
                currentDomain.SetData("TargetFrameworkName", targetFrameworkName);
            }
        }

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

        assembly = Common.ReadFromDiskCache(tempBasePath, requestedAssemblyName);
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
