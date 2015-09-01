using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Mono.Cecil;
using NUnit.Framework;

[TestFixture]
public class CultureResourceTest
{
    Assembly assembly;

    public CultureResourceTest()
    {
        Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");

        var beforeAssemblyPath = Path.GetFullPath(@"..\..\..\AssemblyToProcess\bin\Debug\AssemblyToProcess.dll");
        var directoryName = Path.GetDirectoryName(@"..\..\..\Debug\");
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
                Path.Combine(directoryName, "AssemblyToReferenceMixed.dll"),
            };

        var assemblyToReferenceDirectory = Path.GetDirectoryName(beforeAssemblyPath.Replace("AssemblyToProcess", "AssemblyToReference"));
        var assemblyToReferenceResources = Directory.GetFiles(assemblyToReferenceDirectory, "*.resources.dll", SearchOption.AllDirectories);
        references.AddRange(assemblyToReferenceResources);

        using (var weavingTask = new ModuleWeaver
            {
                ModuleDefinition = moduleDefinition,
                AssemblyResolver = new MockAssemblyResolver(),
                Config = XElement.Parse("<Costura Unmanaged32Assemblies='AssemblyToReferenceMixed' />"),
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