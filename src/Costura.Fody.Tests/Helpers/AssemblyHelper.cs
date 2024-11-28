#if NETCORE

using System;
using System.IO;
using System.Reflection.PortableExecutable;

internal static class AssemblyHelper
{
    public static bool IsManagedAssembly(string assemblyPath)
    {
        try
        {
            using var fileStream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(fileStream);

            if (!peReader.HasMetadata)
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;    
        }

        // Assume true
        return true;
    }
}

#endif
