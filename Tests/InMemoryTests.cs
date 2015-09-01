using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using ApprovalTests;
using ApprovalTests.Reporters;
using Mono.Cecil;
using NUnit.Framework;

[TestFixture]
[UseReporter(typeof(VisualStudioReporter))]
public class InMemoryTests
{
    Assembly assembly;
    string beforeAssemblyPath;
    string afterAssemblyPath;
    ModuleDefinition moduleDefinition;
    string isolatedPath;

    public InMemoryTests()
    {
        beforeAssemblyPath = Path.GetFullPath(@"..\..\..\AssemblyToProcess\bin\Debug\AssemblyToProcess.dll");
        var directoryName = Path.GetDirectoryName(@"..\..\..\Debug\");
#if (!DEBUG)
        beforeAssemblyPath = beforeAssemblyPath.Replace("Debug", "Release");
        directoryName = directoryName.Replace("Debug", "Release");
#endif

        afterAssemblyPath = beforeAssemblyPath.Replace(".dll", "InMemory.dll");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);
        File.Copy(beforeAssemblyPath.Replace(".dll", ".pdb"), afterAssemblyPath.Replace(".dll", ".pdb"), true);

        var readerParams = new ReaderParameters { ReadSymbols = true };

        moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath, readerParams);

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
                Config = XElement.Parse("<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' PreloadOrder='AssemblyToReferenceNative' />"),
                ReferenceCopyLocalPaths = references,
                AssemblyFilePath = beforeAssemblyPath
            })
        {
            weavingTask.Execute();
            var writerParams = new WriterParameters { WriteSymbols = true };
            moduleDefinition.Write(afterAssemblyPath, writerParams);
        }

        isolatedPath = Path.Combine(Path.GetTempPath(), "CosturaIsolatedMemory.dll");
        File.Copy(afterAssemblyPath, isolatedPath, true);
        File.Copy(afterAssemblyPath.Replace(".dll", ".pdb"), isolatedPath.Replace(".dll", ".pdb"), true);
        assembly = Assembly.LoadFile(isolatedPath);
    }

    [Test]
    public void Simple()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.Foo());
    }

    [Test]
    public void SimplePreEmbed()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.Foo2());
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
        Assert.AreEqual(1, moduleDefinition.AssemblyReferences.Count(x => x.Name == "mscorlib"));
    }

    [Test]
    public void EnsureCompilerGeneratedAttribute()
    {
        Assert.IsTrue(moduleDefinition.GetType("Costura.AssemblyLoader").Resolve().CustomAttributes.Any(attr => attr.AttributeType.Name == "CompilerGeneratedAttribute"));
    }

#if DEBUG
    [Test]
    public void TemplateHasCorrectSymbols()
    {
        Approvals.Verify(Decompiler.Decompile(afterAssemblyPath, "Costura.AssemblyLoader"));
        //Approvals.Verify(Decompiler.Decompile(Path.GetFullPath(@"..\..\..\Template\bin\Debug\Template.dll"), "Common"));
    }
#endif

    [Test]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }

    [Test]
    public void TypeReferencedWithPartialAssemblyNameIsLoadedFromExistingAssemblyInstance()
    {
        var instance = assembly.GetInstance("ClassToTest");
        var assemblyLoadedByCompileTimeReference = instance.GetReferencedAssembly();
        var typeLoadedWithPartialAssemblyName = Type.GetType("ClassToReference, AssemblyToReference");
        Assume.That(typeLoadedWithPartialAssemblyName, Is.Not.Null);

        Assert.AreSame(assemblyLoadedByCompileTimeReference, typeLoadedWithPartialAssemblyName.Assembly);
    }
}