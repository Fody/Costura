using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using System.Linq;


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
#if (!DEBUG)

        beforeAssemblyPath = beforeAssemblyPath.Replace("Debug", "Release");
#endif

        afterAssemblyPath = beforeAssemblyPath.Replace(".dll", "TempFile.dll");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);

        var directoryName = Path.GetDirectoryName(beforeAssemblyPath);


        moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath);
        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition,
            AssemblyResolver = new MockAssemblyResolver(),
            CreateTemporaryAssemblies = true,
            ReferenceCopyLocalPaths = new List<string>
                {
                    Path.Combine(directoryName, "AssemblyToReference.dll"),
                    Path.Combine(directoryName, "AssemblyToReferencePreEmbed.dll"),
                }
        };

        weavingTask.Execute();
        moduleDefinition.Write(afterAssemblyPath);

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
        var moduleDefinition = ModuleDefinition.ReadModule(assembly.CodeBase.Remove(0, 8));
        Assert.AreEqual(1, moduleDefinition.AssemblyReferences.Count(x => x.Name == "mscorlib"));
    }


    [Test]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath,afterAssemblyPath);
    }

}