﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using ApprovalTests.Reporters;
using Mono.Cecil;
using NUnit.Framework;

[TestFixture]
[UseReporter(typeof(VisualStudioReporter))]
public class PureDotNetAssemblyTests
{
    Assembly assembly;
    string beforeAssemblyPath;
    string afterAssemblyPath;
    ModuleDefinition moduleDefinition;

    public PureDotNetAssemblyTests()
    {
        // Figure out whether we're in bin\debug, bin\release or bin\debug (mono)
        // All projects will build in the same configuration, so we should know from the project's current
        // configuration

        var directory = Environment.CurrentDirectory;
        var directoryParts = directory.Split(Path.DirectorySeparatorChar);
        var suffix = string.Join(Path.DirectorySeparatorChar.ToString(), directoryParts.Reverse().Take(2).Reverse().ToArray());

        beforeAssemblyPath = Path.GetFullPath(Path.Combine("..", "..", "..", "AssemblyToProcessWithoutUnmanaged", suffix, "AssemblyToProcessWithoutUnmanaged.dll"));
        var directoryName = Path.GetDirectoryName(Path.Combine("..", "..", "..", "Debug"));

#if (!DEBUG)
        beforeAssemblyPath = beforeAssemblyPath.Replace("Debug", "Release");
        directoryName = directoryName.Replace("Debug", "Release");
#endif

        afterAssemblyPath = beforeAssemblyPath.Replace(".dll", "InMemory.dll");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);
        File.Copy(beforeAssemblyPath.Replace(".dll", GetSymbolFileExtension()), afterAssemblyPath.Replace(".dll", GetSymbolFileExtension()), true);

        var readerParams = new ReaderParameters { ReadSymbols = true };

        moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath, readerParams);

        var references = new List<string>
            {
                beforeAssemblyPath.Replace("AssemblyToProcessWithoutUnmanaged", "AssemblyToReference"),
            };

        var assemblyToReferenceDirectory = Path.GetDirectoryName(beforeAssemblyPath.Replace("AssemblyToProcessWithoutUnmanaged", "AssemblyToReference"));
        var assemblyToReferenceResources = Directory.GetFiles(assemblyToReferenceDirectory, "*.resources.dll", SearchOption.AllDirectories);
        references.AddRange(assemblyToReferenceResources);

        // This should use ILTemplate instead of ILTemplateWithUnmanagedHandler.
        using (var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition,
            AssemblyResolver = new MockAssemblyResolver(),
            Config = XElement.Parse("<Costura />"),
            ReferenceCopyLocalPaths = references,
            AssemblyFilePath = beforeAssemblyPath
        })
        {
            weavingTask.Execute();
            var writerParams = new WriterParameters { WriteSymbols = true };
            moduleDefinition.Write(afterAssemblyPath, writerParams);
        }

        var isolatedPath = Path.Combine(Path.GetTempPath(), "CosturaPureDotNetIsolatedMemory.dll");
        File.Copy(afterAssemblyPath, isolatedPath, true);
        File.Copy(afterAssemblyPath.Replace(".dll", GetSymbolFileExtension()), isolatedPath.Replace(".dll", GetSymbolFileExtension()), true);
        assembly = Assembly.LoadFile(isolatedPath);
    }

    [Test]
    public void Simple()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.Foo());
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
    public void EnsureOnly1RefToMscorLib()
    {
        Assert.AreEqual(1, moduleDefinition.AssemblyReferences.Count(x => x.Name == "mscorlib"));
    }

    [Test]
    public void EnsureNoReferenceToTemplate()
    {
        Assert.AreEqual(0, moduleDefinition.AssemblyReferences.Count(x => x.Name == "Template"));
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

    [Test]
    public void TypeReferencedWithPartialAssemblyNameIsLoadedFromExistingAssemblyInstance()
    {
        var instance = assembly.GetInstance("ClassToTest");

        var assemblyLoadedByCompileTimeReference = instance.GetReferencedAssembly();
        var typeLoadedWithPartialAssemblyName = Type.GetType("ClassToReference, AssemblyToReference");
        Assume.That(typeLoadedWithPartialAssemblyName, Is.Not.Null);

        Assert.AreSame(assemblyLoadedByCompileTimeReference, typeLoadedWithPartialAssemblyName.Assembly);
    }

    private static string GetSymbolFileExtension()
    {
        if (!IsRunningOnMono())
        {
            return ".pdb";
        }
        else
        {
            return ".dll.mdb";
        }
    }

    private static bool IsRunningOnMono()
    {
        return Type.GetType("Mono.Runtime") != null;
    }
}