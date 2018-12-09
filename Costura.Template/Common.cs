using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
// ReSharper disable CommentTypo

static class Common
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr LoadLibrary(string dllToLoad);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetDllDirectory(string lpPathName);

    [Conditional("DEBUG")]
    public static void Log(string format, params object[] args)
    {
        // Should this be trace?
        Debug.WriteLine("=== COSTURA === " + string.Format(format, args));
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

    static void CreateDirectory(string tempBasePath)
    {
        if (!Directory.Exists(tempBasePath))
        {
            Directory.CreateDirectory(tempBasePath);
        }
    }

    static byte[] ReadStream(Stream stream)
    {
        var data = new byte[stream.Length];
        stream.Read(data, 0, data.Length);
        return data;
    }

    public static string CalculateChecksum(string filename)
    {
        using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var bs = new BufferedStream(fs))
        using (var sha1 = new SHA1CryptoServiceProvider())
        {
            var hash = sha1.ComputeHash(bs);
            var formatted = new StringBuilder(2 * hash.Length);
            foreach (var b in hash)
            {
                formatted.AppendFormat("{0:X2}", b);
            }
            return formatted.ToString();
        }
    }

    public static Assembly ReadExistingAssembly(AssemblyName name)
    {
        var currentDomain = AppDomain.CurrentDomain;
        var assemblies = currentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var currentName = assembly.GetName();
            if (string.Equals(currentName.Name, name.Name, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(CultureToString(currentName.CultureInfo), CultureToString(name.CultureInfo), StringComparison.InvariantCultureIgnoreCase))
            {
                Log("Assembly '{0}' already loaded, returning existing assembly", assembly.FullName);

                return assembly;
            }
        }
        return null;
    }

    static string CultureToString(CultureInfo culture)
    {
        if (culture == null)
        {
            return "";
        }

        return culture.Name;
    }

    public static Assembly ReadFromDiskCache(string tempBasePath, AssemblyName requestedAssemblyName)
    {
        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !string.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
        {
            name = $"{requestedAssemblyName.CultureInfo.Name}.{name}";
        }

        var bittyness = IntPtr.Size == 8 ? "64" : "32";
        var assemblyTempFilePath = Path.Combine(tempBasePath, string.Concat(name, ".dll"));
        if (File.Exists(assemblyTempFilePath))
        {
            return Assembly.LoadFile(assemblyTempFilePath);
        }
        assemblyTempFilePath = Path.ChangeExtension(assemblyTempFilePath, "exe");
        if (File.Exists(assemblyTempFilePath))
        {
            return Assembly.LoadFile(assemblyTempFilePath);
        }
        assemblyTempFilePath = Path.Combine(Path.Combine(tempBasePath, bittyness), string.Concat(name, ".dll"));
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

    public static Assembly ReadFromEmbeddedResources(Dictionary<string, string> assemblyNames, Dictionary<string, string> symbolNames, AssemblyName requestedAssemblyName)
    {
        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !string.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
        {
            name = $"{requestedAssemblyName.CultureInfo.Name}.{name}";
        }

        byte[] assemblyData;
        using (var assemblyStream = LoadStream(assemblyNames, name))
        {
            if (assemblyStream == null)
            {
                return null;
            }
            assemblyData = ReadStream(assemblyStream);
        }

        using (var pdbStream = LoadStream(symbolNames, name))
        {
            if (pdbStream != null)
            {
                var pdbData = ReadStream(pdbStream);
                return Assembly.Load(assemblyData, pdbData);
            }
        }

        return Assembly.Load(assemblyData);
    }

    static Stream LoadStream(Dictionary<string, string> resourceNames, string name)
    {
        if (resourceNames.TryGetValue(name, out var value))
        {
            return LoadStream(value);
        }

        return null;
    }

    static Stream LoadStream(string fullName)
    {
        var executingAssembly = Assembly.GetExecutingAssembly();

        if (fullName.EndsWith(".compressed"))
        {
            using (var stream = executingAssembly.GetManifestResourceStream(fullName))
            using (var compressStream = new DeflateStream(stream, CompressionMode.Decompress))
            {
                var memStream = new MemoryStream();
                CopyTo(compressStream, memStream);
                memStream.Position = 0;
                return memStream;
            }
        }

        return executingAssembly.GetManifestResourceStream(fullName);
    }

    public static void PreloadUnmanagedLibraries(string hash, string tempBasePath, List<string> libs, Dictionary<string, string> checksums)
    {
        // since tempBasePath is per user, the mutex can be per user
        var mutexId = $"Costura{hash}";

        using (var mutex = new Mutex(false, mutexId))
        {
            var hasHandle = false;
            try
            {
                try
                {
                    hasHandle = mutex.WaitOne(60000, false);
                    if (hasHandle == false)
                    {
                        throw new TimeoutException("Timeout waiting for exclusive access");
                    }
                }
                catch (AbandonedMutexException)
                {
                    hasHandle = true;
                }

                var bittyness = IntPtr.Size == 8 ? "64" : "32";
                CreateDirectory(Path.Combine(tempBasePath, bittyness));
                InternalPreloadUnmanagedLibraries(tempBasePath, libs, checksums);
            }
            finally
            {
                if (hasHandle)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }

    static void InternalPreloadUnmanagedLibraries(string tempBasePath, IList<string> libs, Dictionary<string, string> checksums)
    {
        string name;

        foreach (var lib in libs)
        {
            name = ResourceNameToPath(lib);

            var assemblyTempFilePath = Path.Combine(tempBasePath, name);

            if (File.Exists(assemblyTempFilePath))
            {
                var checksum = CalculateChecksum(assemblyTempFilePath);
                if (checksum != checksums[lib])
                {
                    File.Delete(assemblyTempFilePath);
                }
            }

            if (!File.Exists(assemblyTempFilePath))
            {
                using (var copyStream = LoadStream(lib))
                using (var assemblyTempFile = File.OpenWrite(assemblyTempFilePath))
                {
                    CopyTo(copyStream, assemblyTempFile);
                }
            }
        }

        SetDllDirectory(tempBasePath);

        // prevent system-generated error message when LoadLibrary is called on a dll with an unmet dependency
        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms680621(v=vs.85).aspx
        //
        // SEM_FAILCRITICALERRORS - The system does not display the critical-error-handler message box. Instead, the system sends the error to the calling process.
        // SEM_NOGPFAULTERRORBOX  - The system does not display the Windows Error Reporting dialog.
        // SEM_NOOPENFILEERRORBOX - The OpenFile function does not display a message box when it fails to find a file. Instead, the error is returned to the caller.
        //
        // return value is the previous state of the error-mode bit flags.
        // ErrorModes.SEM_FAILCRITICALERRORS | ErrorModes.SEM_NOGPFAULTERRORBOX | ErrorModes.SEM_NOOPENFILEERRORBOX;
        uint errorModes = 32771;
        var originalErrorMode = SetErrorMode(errorModes);

        foreach (var lib in libs)
        {
            name = ResourceNameToPath(lib);

            if (name.EndsWith(".dll"))
            {
                var assemblyTempFilePath = Path.Combine(tempBasePath, name);

                LoadLibrary(assemblyTempFilePath);
            }
        }

        // restore to previous state
        SetErrorMode(originalErrorMode);
    }

    [DllImport("kernel32.dll")]
    static extern uint SetErrorMode(uint uMode);
    static string ResourceNameToPath(string lib)
    {
        var bittyness = IntPtr.Size == 8 ? "64" : "32";

        var name = lib;

        if (lib.StartsWith(string.Concat("costura", bittyness, ".")))
        {
            name = Path.Combine(bittyness, lib.Substring(10));
        }
        else if (lib.StartsWith("costura."))
        {
            name = lib.Substring(8);
        }

        if (name.EndsWith(".compressed"))
        {
            name = name.Substring(0, name.Length - 11);
        }

        return name;
    }
}