using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Collections.Generic;
using Mono.Cecil;

public abstract class BaseCostura
{
    protected string beforeAssemblyPath;
    protected string afterAssemblyPath;
    protected Assembly assembly;

    protected abstract string Suffix { get; }

    protected void CreateIsolatedAssemblyCopy(string projectName, string config, IEnumerable<string> references, string extension = ".exe")
    {
		var processingDirectory = TestPaths.GetProcessingDirectory(projectName);

        beforeAssemblyPath = Path.Combine(processingDirectory, projectName + extension);

        afterAssemblyPath = beforeAssemblyPath.Replace(extension, Suffix + extension);
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);
        File.Copy(TestPaths.GetSymbolFileName(beforeAssemblyPath), TestPaths.GetSymbolFileName(afterAssemblyPath), true);

        var readerParams = new ReaderParameters { ReadSymbols = true };

        var moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath, readerParams);

        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition,
            AssemblyResolver = new MockAssemblyResolver(),
            Config = XElement.Parse(config),
            ReferenceCopyLocalPaths = references.Select(r => Path.Combine(processingDirectory, r)).ToList(),
            AssemblyFilePath = beforeAssemblyPath
        };

        weavingTask.Execute();
        var writerParams = new WriterParameters { WriteSymbols = true };
        moduleDefinition.Write(afterAssemblyPath, writerParams);

        Directory.CreateDirectory(Suffix);
        var isolatedPath = Path.GetFullPath(Path.Combine(Suffix, $"Costura{Suffix}.exe"));
        File.Copy(afterAssemblyPath, isolatedPath, true);
        File.Copy(TestPaths.GetSymbolFileName(afterAssemblyPath), TestPaths.GetSymbolFileName(isolatedPath), true);
    }

    protected void LoadAssemblyIntoAppDomain()
    {
        var isolatedPath = Path.GetFullPath(Path.Combine(Suffix, $"Costura{Suffix}.exe"));

        assembly = Assembly.LoadFile(isolatedPath);
    }
}
