using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Mono.Cecil;
using NUnit.Framework;
using System;

[TestFixture]
public class CultureResourceTest
{
    Assembly assembly;

    public CultureResourceTest()
    {
        Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");

        // Figure out whether we're in bin\debug, bin\release or bin\debug (mono)
        // All projects will build in the same configuration, so we should know from the project's current
        // configuration

		var directory = Path.GetDirectoryName(typeof(CultureResourceTest).Assembly.Location);
        var directoryParts = directory.Split(Path.DirectorySeparatorChar);
        var suffix = string.Join(Path.DirectorySeparatorChar.ToString(), directoryParts.Reverse().Take(2).Reverse().ToArray());

        var beforeAssemblyPath = Path.GetFullPath(Path.Combine(directory, "..", "..", "..", "AssemblyToProcess", suffix, "AssemblyToProcess.dll"));
        var directoryName = Path.GetDirectoryName(Path.Combine(directory, "..", "..", "..", "Debug"));
#if (!DEBUG)
        beforeAssemblyPath = beforeAssemblyPath.Replace("Debug", "Release");
        directoryName = directoryName.Replace("Debug", "Release");
#endif

        var afterAssemblyPath = beforeAssemblyPath.Replace(".dll", "Culture.dll");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);

        var moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath);

        var references = new List<string>
            {
                beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReference"),
                beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReferencePreEmbed"),
                Path.ChangeExtension(beforeAssemblyPath.Replace("AssemblyToProcess", "ExeToReference"), "exe"),
#if MONO
#else
                Path.Combine(directoryName, "AssemblyToReferenceMixed.dll"),
#endif
            };

        var assemblyToReferenceDirectory = Path.GetDirectoryName(beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReference"));
        var assemblyToReferenceResources = Directory.GetFiles(assemblyToReferenceDirectory, "*.resources.dll", SearchOption.AllDirectories);
        references.AddRange(assemblyToReferenceResources);

        using (var weavingTask = new ModuleWeaver
            {
                ModuleDefinition = moduleDefinition,
                AssemblyResolver = new MockAssemblyResolver(),
#if MONO
#else
                Config = XElement.Parse("<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />"),
#endif
                ReferenceCopyLocalPaths = references,
                AssemblyFilePath = beforeAssemblyPath
            })
        {
            weavingTask.Execute();
            moduleDefinition.Write(afterAssemblyPath);
        }

        var isolatedPath = Path.Combine(Path.GetTempPath(), "CosturaCulture.dll");
        File.Copy(afterAssemblyPath, isolatedPath, true);
        assembly = Assembly.LoadFile(isolatedPath);
    }

    [Test]
    public void UsingResource()
    {
        var instance1 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Salut", instance1.InternationalFoo());
    }
}