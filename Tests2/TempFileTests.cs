using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Mono.Cecil;
using NUnit.Framework;

[TestFixture]
public class TempFileTests
{
    Assembly assembly;
    string beforeAssemblyPath;
    string afterAssemblyPath;
    ModuleDefinition moduleDefinition;
    string isolatedPath;

    public TempFileTests()
    {
        beforeAssemblyPath = Path.GetFullPath(@"..\..\..\AssemblyToProcess\bin\Debug\AssemblyToProcess.dll");
        var directoryName = Path.GetDirectoryName(@"..\..\..\Debug\");
#if (!DEBUG)
        beforeAssemblyPath = beforeAssemblyPath.Replace("Debug", "Release");
        directoryName = directoryName.Replace("Debug", "Release");
#endif

        afterAssemblyPath = beforeAssemblyPath.Replace(".dll", "TempFile.dll");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);

        moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath);

        var references = new List<string>
            {
                beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReference"),
                beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReferencePreEmbed"),
                Path.ChangeExtension(beforeAssemblyPath.Replace("AssemblyToProcess", "ExeToReference"), "exe"),
                Path.Combine(directoryName, "AssemblyToReferenceMixed.dll"),
            };

        var assemblyToReferenceDirectory = Path.GetDirectoryName(beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReference"));
        var assemblyToReferenceResources = Directory.GetFiles(assemblyToReferenceDirectory, "*.resources.dll", SearchOption.AllDirectories);
        references.AddRange(assemblyToReferenceResources);

        using (var weavingTask = new ModuleWeaver
            {
                ModuleDefinition = moduleDefinition,
                AssemblyResolver = new MockAssemblyResolver(),
                Config = XElement.Parse("<Costura CreateTemporaryAssemblies='true' Unmanaged32Assemblies='AssemblyToReferenceMixed' />"),
                ReferenceCopyLocalPaths = references,
                AssemblyFilePath = beforeAssemblyPath
            })
        {
            weavingTask.Execute();
            moduleDefinition.Write(afterAssemblyPath);
        }

        isolatedPath = Path.Combine(Path.GetTempPath(), "CosturaIsolatedTemp.dll");
        File.Copy(afterAssemblyPath, isolatedPath, true);
        assembly = Assembly.LoadFile(isolatedPath);
    }

    [Test]
    public void Simple()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.Foo());
    }

    [Test]
    public void SimplePreEmbed()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.Foo2());
    }

    [Test]
    public void Exe()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.ExeFoo());
    }

    [Test]
    public void ThrowException()
    {
        try
        {
            var instance = assembly.GetInstance("ClassToTest");
            instance.ThrowException();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.StackTrace);
            Assert.IsTrue(exception.StackTrace.Contains("ClassToReference.cs:line"));
        }
    }

    [Test]
    public void Native()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.NativeFoo());
    }

    [Test]
    public void Mixed()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFoo());
    }

    [Test]
    public void MixedPInvoke()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance1.MixedFooPInvoke());
    }

    [Test]
    public void EnsureOnly1RefToMscorLib()
    {
        var moduleDefinition = ModuleDefinition.ReadModule(assembly.CodeBase.Remove(0, 8));
        Assert.AreEqual(1, moduleDefinition.AssemblyReferences.Count(x => x.Name == "mscorlib"));
    }

    [Test]
    public void EnsureCompilerGeneratedAttribute()
    {
        Assert.IsTrue(moduleDefinition.GetType("Costura.AssemblyLoader").Resolve().CustomAttributes.Any(attr => attr.AttributeType.Name == "CompilerGeneratedAttribute"));
    }

    [Test]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }
}