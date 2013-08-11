using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

static class ILTemplateWithUnmanagedHandler
{
    private static string tempBasePath;

    readonly static Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    readonly static Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    readonly static List<string> preload32List = new List<string>();
    readonly static List<string> preload64List = new List<string>();

    public static void Attach()
    {
        //Create a unique Temp directory for the application path.
        var md5Hash = "To be replaced at compile time";
        var prefixPath = Path.Combine(Path.GetTempPath(), "Costura");
        tempBasePath = Path.Combine(prefixPath, md5Hash);

        // Preload
        var unmanagedAssemblies = IntPtr.Size == 8 ? preload64List : preload32List;
        Common.PreloadUnmanagedLibraries(md5Hash, tempBasePath, unmanagedAssemblies);

        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
    }

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name.ToLowerInvariant();

        var existingAssembly = Common.ReadExistingAssembly(name);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        existingAssembly = Common.ReadFromDiskCache(tempBasePath, name);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        return Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, name);
    }
}
