namespace Costura.Fody.Tests
{
    using System;
    using System.IO;
    using global::Fody;
    using NUnit.Framework;

    public class RuntimeReferencesTests : BaseCosturaTest
    {
        private static readonly TestResult testResult;

        static RuntimeReferencesTests()
        {
            testResult = WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
                "<Costura IncludeAssemblies='AssemblyToReferenceWithRuntimeReferences' />",
                new[] { "AssemblyToReferenceWithRuntimeReferences.dll" },
                "RuntimeReferences");
        }

        public override TestResult TestResult => testResult;

        [Test]
        public void UseRuntimeReferences()
        {
            DeleteRuntimeReferencesFolder();

            var instance = TestResult.GetInstance("ClassToTest");
            Assert.AreEqual("Hello", instance.RuntimeReferences());
        }

        private void DeleteRuntimeReferencesFolder()
        {
            // Because we can't use private assets for project references, we have to delete the runtimes folder manually

            var location = Path.GetDirectoryName(GetType().Assembly.Location);
            var runtimesFolder = Path.Combine(location, "runtimes");

            try
            {
                Directory.Delete(runtimesFolder, true);
            }
            catch (Exception)
            {
                // Some libs might not be deleted, but the runtime references have been deleted
            }
        }
    }
}
