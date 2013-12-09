using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

static class ILTemplateWithUnmanagedHandler
{
    static string tempBasePath;

    readonly static Dictionary<string, string> assemblyNames = new Dictionary<string, string>();
    readonly static Dictionary<string, string> symbolNames = new Dictionary<string, string>();

    readonly static List<string> preload32List = new List<string>();
    readonly static List<string> preload64List = new List<string>();

    readonly static Dictionary<string, string> checksums = new Dictionary<string, string>();

    static AssemblyName[] referencedAssemblies;

    public static void Attach()
    {
        referencedAssemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();

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

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        var requestedAssemblyName = new AssemblyName(args.Name);

        foreach (var assembly in referencedAssemblies)
        {
            if (assembly.Name == requestedAssemblyName.Name && assembly.Version != requestedAssemblyName.Version)
            {
                return Assembly.Load(assembly);
            }
        }

        var existingAssembly = Common.ReadExistingAssembly(requestedAssemblyName);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !String.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
            name = String.Format("{0}.{1}", requestedAssemblyName.CultureInfo.Name, name);

        existingAssembly = Common.ReadFromDiskCache(tempBasePath, name);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        return Common.ReadFromEmbeddedResources(assemblyNames, symbolNames, name);
    }
}