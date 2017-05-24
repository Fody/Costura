using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

static class ILTemplateWithUnmanagedHandler
{
    static readonly object nullCacheLock = new object();
    static readonly Dictionary<string, bool> nullCache = new Dictionary<string, bool>();

    static string tempBasePath;

    static readonly Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    static readonly Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    static readonly List<string> preload32List = new List<string>();
    static readonly List<string> preload64List = new List<string>();

    static readonly Dictionary<string, string> checksums = new Dictionary<string, string>();

    static int isAttached = 0;

    public static void Attach()
    {
        if (Interlocked.Exchange(ref isAttached, 1) == 1)
        {
            return;
        }

        //Create a unique Temp directory for the application path.
        var md5Hash = "To be replaced at compile time";
        var prefixPath = Path.Combine(Path.GetTempPath(), "Costura");
        tempBasePath = Path.Combine(prefixPath, md5Hash);

        // Preload
        var unmanagedAssemblies = IntPtr.Size == 8 ? preload64List : preload32List;
        Common.PreloadUnmanagedLibraries(md5Hash, tempBasePath, unmanagedAssemblies, checksums);

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
        if (assembly != null)
        {
            return assembly;
        }

        Common.Log("Loading assembly '{0}' into the AppDomain", requestedAssemblyName);

        assembly = Common.ReadFromDiskCache(tempBasePath, requestedAssemblyName);
        if (assembly != null)
        {
            return assembly;
        }

        assembly = Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, requestedAssemblyName);
        if (assembly == null)
        {
            lock (nullCacheLock)
            {
                nullCache[e.Name] = true;
            }

            // Handles retargeted assemblies like PCL
            if (requestedAssemblyName.Flags == AssemblyNameFlags.Retargetable)
            {
                assembly = Assembly.Load(requestedAssemblyName);
            }
        }
        return assembly;
    }
}