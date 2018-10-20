using System;
using Xunit;

namespace IntegrationTests
{
    using System.IO;
    using System.Linq;

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
            Assert.Equal(embeddedAssembly.CodeBase, thisAssembly.CodeBase, StringComparer.OrdinalIgnoreCase);

            var targetDir = Path.GetDirectoryName(new Uri(thisAssembly.CodeBase).LocalPath);

            var localCopyFiles = Directory.EnumerateFiles(targetDir)
                .Select(Path.GetFileName)
                .ToArray();

            Assert.DoesNotContain(localCopyFiles, file => file.StartsWith("TomsToolbox", StringComparison.OrdinalIgnoreCase));
        }
    }
}
