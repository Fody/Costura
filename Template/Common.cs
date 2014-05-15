using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

static class Common
{
    private const int DelayUntilReboot = 4;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string dllToLoad);

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
        var data = new Byte[stream.Length];
        stream.Read(data, 0, data.Length);
        return data;
    }

    public static string CalculateChecksum(string filename)
    {
        using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (BufferedStream bs = new BufferedStream(fs))
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                byte[] hash = sha1.ComputeHash(bs);
                StringBuilder formatted = new StringBuilder(2 * hash.Length);
                foreach (byte b in hash)
                {
                    formatted.AppendFormat("{0:X2}", b);
                }
                return formatted.ToString();
            }
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
                object.Equals(currentName.CultureInfo, name.CultureInfo))
            {
                Log("Assembly '{0}' already loaded, returning existing assembly", assembly.FullName);

                return assembly;
            }
        }
        return null;
    }

    public static Assembly ReadFromDiskCache(string tempBasePath, AssemblyName requestedAssemblyName)
    {
        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !String.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
            name = String.Format("{0}.{1}", requestedAssemblyName.CultureInfo.Name, name);

        var bittyness = IntPtr.Size == 8 ? "64" : "32";
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
        assemblyTempFilePath = Path.Combine(Path.Combine(tempBasePath, bittyness), String.Concat(name, ".dll"));
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

    public static Assembly ReadFromEmbeddedResources(Dictionary<string, object> resourceNameCache, Dictionary<string, string> assemblyNames, Dictionary<string, string> symbolNames, AssemblyName requestedAssemblyName)
    {
        var name = requestedAssemblyName.Name.ToLowerInvariant();

        if (requestedAssemblyName.CultureInfo != null && !String.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
            name = String.Format("{0}.{1}", requestedAssemblyName.CultureInfo.Name, name);

        object existingAssembly;
        if (resourceNameCache.TryGetValue(name, out existingAssembly)) return (Assembly)existingAssembly;

        // THREADING: Can this be reached concurrently or reentrantly?
        // Concurrency may corrupt the dictionary, and reentrancy may cause repeated loads.
        // Add(...) is used instead of []= to make bugs fail loudly.
        var assembly = LoadAssemblyByResourceName(assemblyNames, symbolNames, name);
        resourceNameCache.Add(name, assembly);
        return assembly;
    }

    private static Assembly LoadAssemblyByResourceName(Dictionary<string, string> assemblyNames, Dictionary<string, string> symbolNames, string name)
    {
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
        string value;
        if (resourceNames.TryGetValue(name, out value))
            return LoadStream(value);

        return null;
    }

    static Stream LoadStream(string fullname)
    {
        var executingAssembly = Assembly.GetExecutingAssembly();

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

    // Mutex code from http://stackoverflow.com/questions/229565/what-is-a-good-pattern-for-using-a-global-mutex-in-c
    public static void PreloadUnmanagedLibraries(string hash, string tempBasePath, IEnumerable<string> libs, Dictionary<string, string> checksums)
    {
        string mutexId = string.Format("Global\\Costura{0}", hash);

        using (var mutex = new Mutex(false, mutexId))
        {
            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);
            mutex.SetAccessControl(securitySettings);

            var hasHandle = false;
            try
            {
                try
                {
                    hasHandle = mutex.WaitOne(5000, false);
                    if (hasHandle == false)
                        throw new TimeoutException("Timeout waiting for exclusive access");
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
                    mutex.ReleaseMutex();
            }
        }
    }

    static void InternalPreloadUnmanagedLibraries(string tempBasePath, IEnumerable<string> libs, Dictionary<string, string> checksums)
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
                    File.Delete(assemblyTempFilePath);
            }

            if (!File.Exists(assemblyTempFilePath))
            {
                using (var copyStream = LoadStream(lib))
                using (var assemblyTempFile = File.OpenWrite(assemblyTempFilePath))
                {
                    CopyTo(copyStream, assemblyTempFile);
                }
                if (!MoveFileEx(assemblyTempFilePath, null, DelayUntilReboot))
                {
                    //TODO: for now we ignore the return value.
                }
            }
        }

        foreach (var lib in libs)
        {
            name = ResourceNameToPath(lib);

            if (name.EndsWith(".dll"))
            {
                var assemblyTempFilePath = Path.Combine(tempBasePath, name);

                LoadLibrary(assemblyTempFilePath);
            }
        }
    }

    static string ResourceNameToPath(string lib)
    {
        var bittyness = IntPtr.Size == 8 ? "64" : "32";

        string name = lib;

        if (lib.StartsWith(String.Concat("costura", bittyness, ".")))
            name = Path.Combine(bittyness, lib.Substring(10));
        else if (lib.StartsWith("costura."))
            name = lib.Substring(8);

        if (name.EndsWith(".zip"))
            name = name.Substring(0, name.Length - 4);

        return name;
    }
}