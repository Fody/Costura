using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Mono.Cecil;
using Mono.Cecil.Pdb;

public abstract class BaseCostura
{
    protected string beforeAssemblyPath;
    protected string afterAssemblyPath;
    protected Assembly assembly;

    protected abstract string Suffix { get; }

    protected void CreateIsolatedAssemblyCopy(string projectName, string config, IEnumerable<string> references, string extension = ".exe")
    {
        var testPath = Assembly.GetAssembly(typeof(BaseCostura)).Location;

        var processingDirectory = Path.GetFullPath(Path.Combine(testPath, $@"..\..\..\..\{projectName}\bin\Debug"));
#if (!DEBUG)
        processingDirectory = processingDirectory.Replace("Debug", "Release");
#endif

        beforeAssemblyPath = Path.Combine(processingDirectory, projectName + extension);
        afterAssemblyPath = beforeAssemblyPath.Replace(extension, Suffix + extension);

        var readerParams = new ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new PdbReaderProvider()
        };
        var moduleDefinition = ModuleDefinition.ReadModule(beforeAssemblyPath, readerParams);

        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition,
            AssemblyResolver = new MockAssemblyResolver(),
            Config = XElement.Parse(config),
            ReferenceCopyLocalPaths = references.Select(r => Path.Combine(processingDirectory, r)).ToList(),
            AssemblyFilePath = beforeAssemblyPath
        };
        weavingTask.Execute();

        var writerParams = new WriterParameters
        {
            WriteSymbols = true,
            SymbolWriterProvider = new PdbWriterProvider()
        };
        moduleDefinition.Write(afterAssemblyPath, writerParams);

        Directory.CreateDirectory(Suffix);
        var isolatedPath = Path.GetFullPath(Path.Combine(Suffix, $"Costura{Suffix}{extension}"));
        File.Copy(afterAssemblyPath, isolatedPath, true);
        File.Copy(afterAssemblyPath.Replace(extension, ".pdb"), isolatedPath.Replace(extension, ".pdb"), true);
    }

    protected void LoadAssemblyIntoAppDomain(string extension = ".exe")
    {
        var isolatedPath = Path.GetFullPath(Path.Combine(Suffix, $"Costura{Suffix}{extension}"));

        assembly = Assembly.LoadFile(isolatedPath);
    }
}