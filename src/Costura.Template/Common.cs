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

internal static class Common
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, uint dwFlags);

    [Conditional("DEBUG")]
    public static void Log(string format, params object[] args)
    {
#if DEBUG
        Console.WriteLine("=== COSTURA === " + string.Format(format, args));
#else
        // Should this be trace?
        Debug.WriteLine("=== COSTURA === " + string.Format(format, args));
#endif
    }

    private static void CopyTo(Stream source, Stream destination)
    {
        var array = new byte[81920];
        int count;
        while ((count = source.Read(array, 0, array.Length)) != 0)
        {
            destination.Write(array, 0, count);
        }
    }

    private static void CreateDirectory(string tempBasePath)
    {
        if (!Directory.Exists(tempBasePath))
        {
            Directory.CreateDirectory(tempBasePath);
        }
    }

    private static byte[] ReadStream(Stream stream)
    {
        var data = new byte[stream.Length];
        stream.Read(data, 0, data.Length);
        return data;
    }

    public static string CalculateChecksum(string filename)
    {
        using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var bs = new BufferedStream(fs))
        using (var sha1 = SHA1.Create())
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

    private static string CultureToString(CultureInfo culture)
    {
        if (culture is null)
        {
            return string.Empty;
        }

        return culture.Name;
    }

    public static Assembly ReadFromDiskCache(string tempBasePath, AssemblyName requestedAssemblyName)
    {
        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo is not null && !string.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
        {
            name = $"{requestedAssemblyName.CultureInfo.Name}.{name}";
        }

        var platformName = GetPlatformName();
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
        assemblyTempFilePath = Path.Combine(Path.Combine(tempBasePath, platformName), string.Concat(name, ".dll"));
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

        if (requestedAssemblyName.CultureInfo is not null && !string.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
        {
            name = $"{requestedAssemblyName.CultureInfo.Name}.{name}";
        }

        byte[] assemblyData;
        using (var assemblyStream = LoadStream(assemblyNames, name))
        {
            if (assemblyStream is null)
            {
                return null;
            }
            assemblyData = ReadStream(assemblyStream);
        }

        using (var pdbStream = LoadStream(symbolNames, name))
        {
            if (pdbStream is not null)
            {
                var pdbData = ReadStream(pdbStream);
                return Assembly.Load(assemblyData, pdbData);
            }
        }

        return Assembly.Load(assemblyData);
    }

    private static Stream LoadStream(Dictionary<string, string> resourceNames, string name)
    {
        if (resourceNames.TryGetValue(name, out var value))
        {
            return LoadStream(value);
        }

        return null;
    }

    private static Stream LoadStream(string fullName)
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

                var platformName = GetPlatformName();

                CreateDirectory(Path.Combine(tempBasePath, platformName));
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

    private static void InternalPreloadUnmanagedLibraries(string tempBasePath, IList<string> libs, Dictionary<string, string> checksums)
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

                // LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
                LoadLibraryEx(assemblyTempFilePath, IntPtr.Zero, 0x00000008);
            }
        }

        // restore to previous state
        SetErrorMode(originalErrorMode);
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);

    private static string ResourceNameToPath(string lib)
    {
        var platformName = GetPlatformName();
        var name = lib;

        if (lib.StartsWith(string.Concat("costura", platformName, ".")))
        {
            name = Path.Combine(platformName, lib.Substring(10));
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

    private static string GetPlatformName()
    {
#if NETCORE
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.Arm64:
                return "arm64";

            case Architecture.X86:
                return "x86";

            case Architecture.X64:
                return "x64";

            default:
                throw new NotSupportedException($"Architecture '{RuntimeInformation.ProcessArchitecture}' not supported");
        }
#else

        var bittyness = IntPtr.Size == 8 ? "64" : "32";
        return $"x{bittyness}";
#endif
    }
}
