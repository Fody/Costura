namespace Costura.Fody.Tests
{
    public class ReferenceTests
    {
        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", "Catel.Core.dll")]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "nl/Catel.Core.resources.dll")]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "runtimes/win-x64/Catel.Core.dll")]
        public async Task RelativePath(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            await Assert.That(reference.RelativeFileName).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", "Catel.Core.dll")]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "nl/Catel.Core.resources.dll")]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "Catel.Core.dll")]
        public async Task RelativePath_UseNonRuntimeReferencePath(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            await Assert.That(reference.RelativeFileName).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", "")]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "")]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "runtimes.win-x64")]
        public async Task RelativePrefix(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            await Assert.That(reference.RelativePrefix).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", "")]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "")]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "")]
        public async Task RelativePrefix_UseNonRuntimeReferencePath(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            await Assert.That(reference.RelativePrefix).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", false)]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", true)]
        public async Task IsRuntimeReference(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            await Assert.That(reference.IsRuntimeReference).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", false)]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public async Task IsRuntimeReference_UseNonRuntimeReferencePath(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            await Assert.That(reference.IsRuntimeReference).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", true)]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public async Task IsResourcesAssembly(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            await Assert.That(reference.IsResourcesAssembly).IsEqualTo(expectedOutput);
        }

        [Test]
        [Arguments(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [Arguments(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", true)]
        [Arguments(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public async Task IsResourcesAssembly_UseNonRuntimeReferencePath(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            await Assert.That(reference.IsResourcesAssembly).IsEqualTo(expectedOutput);
        }
    }
}
