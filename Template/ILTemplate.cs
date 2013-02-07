using System;
using System.IO;
using System.Reflection;

internal static class ILTemplate
{
    public static void Attach()
    {
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += ResolveAssembly;
    }

    public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name.ToLowerInvariant();
        var existingAssembly = ReadExistingAssembly(name);
        if (existingAssembly != null)
        {
            return existingAssembly;
        }

        var prefix = string.Concat("costura.", name);
        var executingAssembly = Assembly.GetExecutingAssembly();

        byte[] assemblyData;
        using (var assemblyStream = GetAssemblyStream(executingAssembly, prefix))
        {
            if (assemblyStream == null)
            {
                return null;
            }
            assemblyData = ReadStream(assemblyStream);
        }

        using (var pdbStream = GetDebugStream(executingAssembly, prefix))
        {
            if (pdbStream != null)
            {
                var pdbData = ReadStream(pdbStream);
                return Assembly.Load(assemblyData, pdbData);
            }
        }

        return Assembly.Load(assemblyData);
    }

    static byte[] ReadStream(Stream atream)
    {
        var data = new Byte[atream.Length];
        atream.Read(data, 0, data.Length);
        return data;
    }

    static Stream GetDebugStream(Assembly executingAssembly, string prefix)
    {
        var pdbName = string.Concat(prefix, ".pdb");
        return executingAssembly.GetManifestResourceStream(pdbName);
    }

    static Stream GetAssemblyStream(Assembly executingAssembly, string prefix)
    {
        var dllName = string.Concat(prefix, ".dll");
        return executingAssembly.GetManifestResourceStream(dllName);
    }

    public static Assembly ReadExistingAssembly(string name)
    {
        var currentDomain = AppDomain.CurrentDomain;
        var assemblies = currentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var fullName = assembly.FullName.ToLowerInvariant();
            var indexOf = fullName.IndexOf(',');
            if (indexOf > 1)
            {
                fullName = fullName.Substring(0, indexOf);
            }

            if (fullName == name)
            {
                return assembly;
            }
        }
        return null;
    }
}
