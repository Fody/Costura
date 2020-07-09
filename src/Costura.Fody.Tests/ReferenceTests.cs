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
            var reference = new Reference(input);

            Assert.AreEqual(expectedOutput, reference.RelativeFileName);
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", "")]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", "")]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", "runtimes.win-x64")]
        public void RelativePrefix(string input, string expectedOutput)
        {
            var reference = new Reference(input);

            Assert.AreEqual(expectedOutput, reference.RelativePrefix);
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", true)]
        public void IsRuntimeReference(string input, bool expectedOutput)
        {
            var reference = new Reference(input);

            Assert.AreEqual(expectedOutput, reference.IsRuntimeReference);
        }

        [TestCase(@"C:\Source\Catel.Core\output\Catel.Core.dll", false)]
        [TestCase(@"C:\Source\Catel.Core\output\nl\Catel.Core.resources.dll", true)]
        [TestCase(@"C:\Source\Catel.Core\output\runtimes\win-x64\Catel.Core.dll", false)]
        public void IsResourcesAssembly(string input, bool expectedOutput)
        {
            var reference = new Reference(input);

            Assert.AreEqual(expectedOutput, reference.IsResourcesAssembly);
        }
    }
}
