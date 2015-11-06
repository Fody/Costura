using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using ApprovalTests;
using ApprovalTests.Namers;
using Mono.Cecil;
using NUnit.Framework;

public abstract class BaseCosturaTest
{
    protected string beforeAssemblyPath;
    protected string afterAssemblyPath;
    protected Assembly assembly;

    protected abstract string Suffix { get; }

    protected void CreateIsolatedAssemblyCopy(string config)
    {
        var processingDirectory = Path.GetFullPath(@"..\..\..\ExeToProcess\bin\Debug");
#if (!DEBUG)
        processingDirectory = processingDirectory.Replace("Debug", "Release");
#endif

        beforeAssemblyPath = Path.Combine(processingDirectory, "ExeToProcess.exe");

        afterAssemblyPath = beforeAssemblyPath.Replace(".exe", $"{Suffix}.exe");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);
        File.Copy(beforeAssemblyPath.Replace(".exe", ".pdb"), afterAssemblyPath.Replace(".exe", ".pdb"), true);

        var readerParams = new ReaderParameters { ReadSymbols = true };

        var moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath, readerParams);

        var references = new List<string>
        {
            Path.Combine(processingDirectory, "AssemblyToReference.dll")
        };

        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition,
            AssemblyResolver = new MockAssemblyResolver(),
            Config = XElement.Parse(config),
            ReferenceCopyLocalPaths = references,
            AssemblyFilePath = beforeAssemblyPath
        };

        weavingTask.Execute();
        var writerParams = new WriterParameters { WriteSymbols = true };
        moduleDefinition.Write(afterAssemblyPath, writerParams);

        Directory.CreateDirectory(Suffix);
        var isolatedPath = Path.GetFullPath(Path.Combine(Suffix, $"Costura{Suffix}.exe"));
        File.Copy(afterAssemblyPath, isolatedPath, true);
        File.Copy(afterAssemblyPath.Replace(".exe", ".pdb"), isolatedPath.Replace(".exe", ".pdb"), true);
    }

    protected void LoadAssemblyIntoAppDomain()
    {
        var isolatedPath = Path.GetFullPath(Path.Combine(Suffix, $"Costura{Suffix}.exe"));

        assembly = Assembly.LoadFile(isolatedPath);
    }

#if DEBUG

    [Test, Category("IL")]
    public void TemplateHasCorrectSymbols()
    {
        using (ApprovalResults.ForScenario(Suffix))
        {
            Approvals.Verify(Decompiler.Decompile(afterAssemblyPath, "Costura.AssemblyLoader"));
        }
    }

#endif

    [Test, Category("IL")]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void Simple()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.Foo());
    }
}