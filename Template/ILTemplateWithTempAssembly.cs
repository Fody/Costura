using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

static class ILTemplateWithTempAssembly
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

    private static string tempBasePath;

    public static void Attach()
    {
        //Create a unique Temp directory for the application path.
        var md5Hash = CreateMd5Hash(Assembly.GetExecutingAssembly().CodeBase);
        var prefixPath = Path.Combine(Path.GetTempPath(), "Costura");
        tempBasePath = Path.Combine(prefixPath, md5Hash);
        CreateDirectory();

        PreloadUnmanagedLibraries();

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

        var assemblyTempFilePath = Path.Combine(tempBasePath, String.Concat(name, ".dll"));
        if (File.Exists(assemblyTempFilePath))
        {
            return Assembly.LoadFile(assemblyTempFilePath);
        }

        return null;
    }

    static void CreateDirectory()
    {
        if (Directory.Exists(tempBasePath))
        {
            try
            {
                Directory.Delete(tempBasePath, true);
            }
            catch
            { }
        }
        Directory.CreateDirectory(tempBasePath);
        MoveFileEx(tempBasePath, null, 0x4);
    }

    static string CreateMd5Hash(string input)
    {
        using (var md5 = MD5.Create())
        {
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);

            var sb = new StringBuilder();
            for (var i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    static byte[] ReadStream(Stream atream)
    {
        var data = new Byte[atream.Length];
        atream.Read(data, 0, data.Length);
        return data;
    }

    static Stream TryFindEmbeddedStream(Assembly executingAssembly, string prefix, string name)
    {
        var fullName = String.Concat(prefix, ".", name);
        var stream = executingAssembly.GetManifestResourceStream(fullName);
        if (stream != null)
            return stream;

        fullName = String.Concat(prefix, ".cmp.", name);
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

        return null;
    }

    static void CopyTo(Stream source, Stream destination)
    {
        byte[] array = new byte[81920];
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

    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string dllToLoad);

    static void PreloadUnmanagedLibraries()
    {
        // Preload correct library
        var bittyness = IntPtr.Size == 8 ? "64" : "32";

        var executingAssembly = Assembly.GetExecutingAssembly();

        string name;

        foreach (var lib in executingAssembly.GetManifestResourceNames())
        {
            if (lib.StartsWith(String.Concat("costura", bittyness, ".cmp.")))
                name = lib.Substring(14);
            else if (lib.StartsWith(String.Concat("costura", bittyness, ".")))
                name = lib.Substring(10);
            else if (lib.StartsWith("costura.cmp."))
                name = lib.Substring(12);
            else if (lib.StartsWith("costura."))
                name = lib.Substring(8);
            else
                continue;

            var assemblyTempFilePath = Path.Combine(tempBasePath, name);

            if (!File.Exists(assemblyTempFilePath))
            {
                var assemblyStream = TryFindEmbeddedStream(executingAssembly, "costura" + bittyness, name);
                if (assemblyStream == null)
                {
                    assemblyStream = TryFindEmbeddedStream(executingAssembly, "costura", name);
                }
                if (assemblyStream == null)
                {
                    continue;
                }

                using (var copyStream = assemblyStream)
                using (var assemblyTempFile = File.OpenWrite(assemblyTempFilePath))
                {
                    CopyTo(copyStream, assemblyTempFile);
                }
            }
        }

        foreach (var lib in executingAssembly.GetManifestResourceNames())
        {
            if (lib.EndsWith(".dll"))
            {
                if (lib.StartsWith(String.Concat("costura", bittyness, ".cmp.")))
                    name = lib.Substring(14);
                else if (lib.StartsWith(String.Concat("costura", bittyness, ".")))
                    name = lib.Substring(10);
                else
                    continue;

                var assemblyTempFilePath = Path.Combine(tempBasePath, name);

                LoadLibrary(assemblyTempFilePath);
            }
        }
    }
}