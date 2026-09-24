namespace Costura.Fody.Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using global::Fody;

    public class RuntimeReferencesTests : BaseCosturaTest
    {
        private static TestResult testResult;

        static TestResult InitializeTest()
        {
            var weavers = @"<Costura>
        <IncludeAssemblies>
            Microsoft.Data.*
        </IncludeAssemblies>
        <IncludeRuntimeAssemblies>
            Microsoft.Data.SqlClient
            Microsoft.Data.SqlClient.SNI
        </IncludeRuntimeAssemblies>
</Costura>";

            // We need to original assembly directory
            var currentDirectory = AssemblyDirectoryHelper.GetCurrentDirectory();
            var assemblyPath = Path.Combine(currentDirectory, "..", "..",
                "AssemblyToReferenceWithRuntimeReferences", "net8.0", "AssemblyToReferenceWithRuntimeReferences.dll");

            assemblyPath = Path.GetFullPath(assemblyPath);

            var testResult = WeavingHelper.CreateIsolatedAssemblyCopy(assemblyPath,
                weavers,
                new[]
                {
                    "Microsoft.Data.SqlClient.dll",
                    "Microsoft.Data.SqlClient.Extensions.Abstractions.dll",
                    "Microsoft.Data.SqlClient.Internal.Logging.dll",

                    Path.Combine("runtimes", "unix", "lib", "net8.0", "Microsoft.Data.SqlClient.dll"),
                    Path.Combine("runtimes", "win-arm64", "native", "Microsoft.Data.SqlClient.SNI.dll"),
                    Path.Combine("runtimes", "win-x64", "native", "Microsoft.Data.SqlClient.SNI.dll"),
                    Path.Combine("runtimes", "win-x86", "native", "Microsoft.Data.SqlClient.SNI.dll"),
                },
                "RuntimeReferences");

            return testResult;
        }

        public override TestResult TestResult => testResult;

        [Test, Explicit]
        public async Task UseRuntimeReferences()
        {
            testResult = InitializeTest();

            DeleteRuntimeReferencesFolder();

            var runtimeReferencesType = TestResult.Assembly.GetType("RuntimeReferences");
            var staticMethod = runtimeReferencesType.GetMethod("UseAssemblyWithRuntimeAssemblies", BindingFlags.Static | BindingFlags.Public);
            await Assert.That(staticMethod.Invoke(null, null)).IsEqualTo("Hello");
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
