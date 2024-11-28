using System.IO;
using System.Linq;
using System.Xml.Linq;
using Costura.Fody.Tests;
using Fody;

public static class WeavingHelper
{
    public static TestResult CreateIsolatedAssemblyCopy(string assemblyPath, string config, string[] references, string assemblyName)
    {
        var currentDirectory = AssemblyDirectoryHelper.GetCurrentDirectory();

        using (var weaver = new ModuleWeaver
        {
            Config = XElement.Parse(config),
            References = string.Join(";", references.Select(r => Path.Combine(currentDirectory, r))),
            ReferenceCopyLocalPaths = references.Select(r => Path.Combine(currentDirectory, r)).ToList(),
        })
        {
            if (!Path.IsPathRooted(assemblyPath))
            {
                assemblyPath = Path.Combine(currentDirectory, assemblyPath);
            }

#if NETCORE
            var shouldCopyExe = false;
            var originalExe = string.Empty;

            // Exe are now native, use .dll instead, but copy the exe
            if (assemblyPath.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!AssemblyHelper.IsManagedAssembly(assemblyPath))
                {
                    shouldCopyExe = true;
                    originalExe = assemblyPath;

                    assemblyPath = Path.ChangeExtension(assemblyPath, ".dll");
                }
            }
#endif

            var assembly = weaver.ExecuteTestRun(
                assemblyPath,
                folderName: assemblyName,
                ignoreCodes: new[] { "0x80131869" },
                runPeVerify: false);

#if NETCORE
            if (shouldCopyExe && !string.IsNullOrWhiteSpace(originalExe))
            {
                var destinationExe = Path.ChangeExtension(assembly.AssemblyPath, ".exe");

                File.Copy(originalExe, destinationExe, true);
            }

            var extensionsToCopy = new[]
            {
                ".runtimeconfig.json",
                ".pdb"
            };

            foreach (var extensionToCopy in extensionsToCopy)
            {
                var targetFile = Path.ChangeExtension(assembly.AssemblyPath, extensionToCopy);
                var sourceFile = Path.Combine(currentDirectory, Path.GetFileName(targetFile));
                
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, targetFile, true);
                }
            }
#endif

            return assembly;
        }
    }
}
