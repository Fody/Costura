    using System;
    using System.IO;
    using System.Linq;

    using Xunit;

namespace IntegrationTests
{
    public class Integration
    {
        [Fact]
        public void Test1()
        {
            // just use some code to ensure assembly is properly embedded.
            var now = DateTime.Now;
            var x = TomsToolbox.Core.DateTimeOperations.Max(now, DateTime.Today);
            Assert.Equal(x, now);

            var embeddedAssembly = typeof(TomsToolbox.Core.DateTimeOperations).Assembly;
            var thisAssembly = GetType().Assembly;

            Assert.Empty(embeddedAssembly.Location);
            // does not work on build server: Assert.Equal(embeddedAssembly.CodeBase, thisAssembly.CodeBase, StringComparer.OrdinalIgnoreCase);

            var targetDir = Path.GetDirectoryName(new Uri(thisAssembly.CodeBase).LocalPath);

            var localCopyFiles = Directory.EnumerateFiles(targetDir)
                .Select(Path.GetFileName)
                .ToArray();

            Assert.Contains(localCopyFiles, file => file.StartsWith("xunit", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(localCopyFiles, file => file.StartsWith("TomsToolbox", StringComparison.OrdinalIgnoreCase));
        }
    }
}
