namespace Costura.Fody.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class ReferenceTests
    {
        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", "Catel.Core.dll")]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "nl/Catel.Core.resources.dll")]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "runtimes/win-x64/Catel.Core.dll")]
        public void RelativePath(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            Assert.That(reference.RelativeFileName, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", "Catel.Core.dll")]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "nl/Catel.Core.resources.dll")]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "Catel.Core.dll")]
        public void RelativePath_UseNonRuntimeReferencePath(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            Assert.That(reference.RelativeFileName, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", "")]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "")]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "runtimes.win-x64")]
        public void RelativePrefix(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            Assert.That(reference.RelativePrefix, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", "")]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "")]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "")]
        public void RelativePrefix_UseNonRuntimeReferencePath(string input, string expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            Assert.That(reference.RelativePrefix, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", true)]
        public void IsRuntimeReference(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            Assert.That(reference.IsRuntimeReference, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public void IsRuntimeReference_UseNonRuntimeReferencePath(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            Assert.That(reference.IsRuntimeReference, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", true)]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public void IsResourcesAssembly(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: true);

            Assert.That(reference.IsResourcesAssembly, Is.EqualTo(expectedOutput));
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", true)]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public void IsResourcesAssembly_UseNonRuntimeReferencePath(string input, bool expectedOutput)
        {
            var reference = new Reference(input, useRuntimeReferencePaths: false);

            Assert.That(reference.IsResourcesAssembly, Is.EqualTo(expectedOutput));
        }
    }
}
