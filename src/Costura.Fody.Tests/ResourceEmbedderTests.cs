namespace Costura.Fody.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class ResourceEmbedderTests
    {
        private const string CacheRoot = "C:\\CI_WS\\Ws\\195338\\Source\\My_SuperLarge_ProjectName_WithTooLongPaths\\src\\My_SuperLarge_ProjectName_With_Additional_SuperPaths\\obj\\Release\\net5.0-windows\\Costura";
        private const string DummyShaChecksum = "123456789123456789123456789";

        [TestCase("shortpath.dll", $"{CacheRoot}\\{DummyShaChecksum}.shortpath.dll.compressed")]
        [TestCase("runtimes.unix.lib.netcoreapp2.1.microsoft.data.sqlclient.dll", $"{CacheRoot}\\{DummyShaChecksum}.1.compressed")]
        public void TheGetCacheFileMethod(string resourceName, string expectedOutput)
        {
            // Just dummy values
            using (var moduleWeaver = new ModuleWeaver())
            {
                var actualOutput = moduleWeaver.GetCacheFile(CacheRoot, resourceName, true, DummyShaChecksum);

                Assert.That(actualOutput, Is.EqualTo(expectedOutput));
            }
        }
    }
}
