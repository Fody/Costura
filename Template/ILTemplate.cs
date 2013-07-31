using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

static class ILTemplate
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

        var executingAssembly = Assembly.GetExecutingAssembly();

        byte[] assemblyData;
        using (var assemblyStream = TryFindEmbeddedStream(executingAssembly, "costura", name, new string[] { ".dll", ".exe" }))
        {
            if (assemblyStream == null)
            {
                return null;
            }
            assemblyData = ReadStream(assemblyStream);
        }

        using (var pdbStream = TryFindEmbeddedStream(executingAssembly, "costura", name, new string[] { ".pdb" }))
        {
            if (pdbStream != null)
            {
                var pdbData = ReadStream(pdbStream);
                return Assembly.Load(assemblyData, pdbData);
            }
        }

        return Assembly.Load(assemblyData);
    }

    static byte[] ReadStream(Stream stream)
    {
        var data = new Byte[stream.Length];
        stream.Read(data, 0, data.Length);
        return data;
    }

    static Stream TryFindEmbeddedStream(Assembly executingAssembly, string prefix, string name, string[] extensions)
    {
        for (int i = 0; i < extensions.Length; i++)
        {
            var fullName = String.Concat(prefix, ".", name, extensions[i]);
            var stream = executingAssembly.GetManifestResourceStream(fullName);
            if (stream != null)
                return stream;

            fullName = String.Concat(prefix, ".", name, extensions[i], ".zip");
            stream = executingAssembly.GetManifestResourceStream(fullName);
            if (stream != null)
            {
                var memStream = new MemoryStream();
                using (var compressStream = new DeflateStream(stream, CompressionMode.Decompress))
                {
                    CopyTo(compressStream, memStream);
                }
                memStream.Position = 0;
                return memStream;
            }
        }

        return null;
    }

    static void CopyTo(Stream source, Stream destination)
    {
        var array = new byte[81920];
        int count;
        while ((count = source.Read(array, 0, array.Length)) != 0)
        {
            destination.Write(array, 0, count);
        }
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
