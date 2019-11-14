using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Costura.Fody.Tests;
using Fody;

public static class WeavingHelper
{
    public static TestResult CreateIsolatedAssemblyCopy(string assemblyPath, string config, string[] references, string assemblyName)
    {
        var weavingTask = new ModuleWeaver
        {
            Config = XElement.Parse(config),
            References = string.Join(";", references.Select(r => Path.Combine(AssemblyDirectoryHelper.GetCurrentDirectory(), r))),
            ReferenceCopyLocalPaths = references.Select(r => Path.Combine(AssemblyDirectoryHelper.GetCurrentDirectory(), r)).ToList(),
            AssemblyResolver = new TestAssemblyResolver()
        };

        if (!Path.IsPathRooted(assemblyPath))
        {
            assemblyPath = Path.Combine(AssemblyDirectoryHelper.GetCurrentDirectory(), assemblyPath);
        }

        return weavingTask.ExecuteTestRun(assemblyPath,
            assemblyName: assemblyName,
            ignoreCodes: new []{ "0x80131869" },
            runPeVerify:false);
    }
}
