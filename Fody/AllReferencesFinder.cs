using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public partial class ModuleWeaver
{
    private void FindAllLocalReferences(Configuration config)
    {
        // We need to resolve everything in another app domain. When we then unload that app domain,m
        // the loaded assemblies will be freed/unlocked. That's a good thing.
        var referenceResolvingAppDomain = AppDomain.CreateDomain("referenceResolvingDomain");
        
        try
        {
            var objectHandle = referenceResolvingAppDomain.CreateInstanceFrom(
                typeof (AllReferenceFinder).Assembly.Location, 
                typeof (AllReferenceFinder).FullName);
            var finder = (AllReferenceFinder) objectHandle.Unwrap();
            var newReferenceCopyLocalPaths = finder.Execute(Path.GetDirectoryName(AssemblyFilePath), ReferenceCopyLocalPaths.ToArray());

            // Will not reassign list. Not sure what is holding the original ReferenceCopyLocalPaths list!
            ReferenceCopyLocalPaths.Clear();
            ReferenceCopyLocalPaths.AddRange(newReferenceCopyLocalPaths);
        }
        finally
        {
            AppDomain.Unload(referenceResolvingAppDomain);
        }
    }

    private class AllReferenceFinder : MarshalByRefObject
    {
        // Make a breadth first search of all dependencies. Only embed the assemblies that
        // may be found locally (in the same directory as the processed assemly).
        public string[] Execute(string outputDirectory, string[] initialReferencePaths)
        {
            var allReferences = new HashSet<string>();
            var referencesToProcess = new Queue<string>(initialReferencePaths);

            while (referencesToProcess.Any())
            {
                var reference = referencesToProcess.Dequeue();
                if (allReferences.Contains(reference))
                    continue;

                allReferences.Add(reference);
                foreach (var childReference in GetLocalReferencedAssemblies(outputDirectory, reference))
                {
                    referencesToProcess.Enqueue(childReference);
                }
            }

            return allReferences.ToArray();
        }

        private IEnumerable<string> GetLocalReferencedAssemblies(string outputDirectory, string assemblyPath)
        {
            var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);

            foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
            {
                var referencedAssemblyPath = Path.Combine(outputDirectory, referencedAssemblyName.Name + ".dll");
                if (!File.Exists(referencedAssemblyPath))
                    referencedAssemblyPath = Path.Combine(outputDirectory, referencedAssemblyName.Name + ".exe");
                if (File.Exists(referencedAssemblyPath))
                    yield return referencedAssemblyPath;
            }
        }
    }
}
