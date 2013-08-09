using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

static class ILTemplateWithTempAssembly
{
    private static string tempBasePath;

    readonly static List<string> preloadList = new List<string>();
    readonly static List<string> preload32List = new List<string>();
    readonly static List<string> preload64List = new List<string>();

    public static void Attach()
    {
        //Create a unique Temp directory for the application path.
        var md5Hash = Common.CreateMd5Hash(Assembly.GetExecutingAssembly().CodeBase);
        var prefixPath = Path.Combine(Path.GetTempPath(), "Costura");
        tempBasePath = Path.Combine(prefixPath, md5Hash);
        Common.CreateDirectory(tempBasePath);

        // Preload
        var unmanagedAssemblies = IntPtr.Size == 8 ? preload64List : preload32List;
        var libList = new List<string>();
        libList.AddRange(unmanagedAssemblies);
        libList.AddRange(preloadList);
        Common.PreloadUnmanagedLibraries(tempBasePath, libList);

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

        return null;
    }
}
