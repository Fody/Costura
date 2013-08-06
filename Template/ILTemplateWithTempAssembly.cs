using System;
using System.Collections.Generic;
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

    readonly static List<string> preloadList = new List<string>();
    readonly static List<string> preload32List = new List<string>();
    readonly static List<string> preload64List = new List<string>();

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
        assemblyTempFilePath = Path.ChangeExtension(assemblyTempFilePath, "exe");
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
                Directory.CreateDirectory(tempBasePath);
            }
            catch
            {
            }
        }
        else
        {
            Directory.CreateDirectory(tempBasePath);
        }
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

    static Stream LoadStream(Assembly executingAssembly, string fullname)
    {
        if (fullname.EndsWith(".zip"))
        {
            using (var stream = executingAssembly.GetManifestResourceStream(fullname))
            using (var compressStream = new DeflateStream(stream, CompressionMode.Decompress))
            {
                var memStream = new MemoryStream();
                CopyTo(compressStream, memStream);
                memStream.Position = 0;
                return memStream;
            }
        }

        return executingAssembly.GetManifestResourceStream(fullname);
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

    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string dllToLoad);

    static void PreloadUnmanagedLibraries()
    {
        // Preload correct library
        var bittyness = IntPtr.Size == 8 ? "64" : "32";

        var executingAssembly = Assembly.GetExecutingAssembly();

        string name;
        var unmanagedAssemblies = IntPtr.Size == 8 ? preload64List : preload32List;

        var libList = new List<string>();
        libList.AddRange(unmanagedAssemblies);
        libList.AddRange(preloadList);

        foreach (var lib in libList)
        {
            if (lib.StartsWith(String.Concat("costura", bittyness, ".")))
                name = lib.Substring(10);
            else if (lib.StartsWith("costura."))
                name = lib.Substring(8);
            else
                continue;

            if (name.EndsWith(".zip"))
                name = name.Substring(0, name.Length - 4);

            var assemblyTempFilePath = Path.Combine(tempBasePath, name);

            if (!File.Exists(assemblyTempFilePath))
            {
                using (var copyStream = LoadStream(executingAssembly, lib))
                using (var assemblyTempFile = File.OpenWrite(assemblyTempFilePath))
                {
                    CopyTo(copyStream, assemblyTempFile);
                }
            }
        }

        foreach (var lib in unmanagedAssemblies)
        {
            name = lib.Substring(10);
            if (name.EndsWith(".zip"))
                name = name.Substring(0, name.Length - 4);

            if (name.EndsWith(".dll"))
            {
                var assemblyTempFilePath = Path.Combine(tempBasePath, name);

                LoadLibrary(assemblyTempFilePath);
            }
        }
    }
}