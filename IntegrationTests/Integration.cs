using System;
using System.IO;
using System.Linq;

#if !NO_XUNIT
using Xunit;
#endif

namespace IntegrationTests
{
    public class Integration
    {
        [Fact]
        public void Test()
        {
            // just use some code from Newtonsoft.Json to ensure it's referenced.
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            Assert.Equal("{\"SomeProperty\":\"Test\"}", json);

            // just use some code to ensure assemblies are properly embedded.
            var now = DateTime.Now;
            var x = TomsToolbox.Core.DateTimeOperations.Max(now, DateTime.Today);
            Assert.Equal(x, now);

            var embeddedAssembly = typeof(TomsToolbox.Core.DateTimeOperations).Assembly;
            var thisAssembly = GetType().Assembly;

            Assert.Empty(embeddedAssembly.Location);
            // does not work on build server: Assert.Equal(embeddedAssembly.CodeBase, thisAssembly.CodeBase, StringComparer.OrdinalIgnoreCase);

            var embeddedAssembly2 = typeof(System.Windows.Interactivity.DefaultTriggerAttribute).Assembly;
            Assert.Empty(embeddedAssembly2.Location);

            var targetDir = Path.GetDirectoryName(new Uri(thisAssembly.CodeBase).LocalPath);

            var localCopyFiles = Directory.EnumerateFiles(targetDir)
                .Select(Path.GetFileName)
                .ToArray();

            Assert.Contains(localCopyFiles, file => file.StartsWith("Newtonsoft", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(localCopyFiles, file => file.StartsWith("TomsToolbox", StringComparison.OrdinalIgnoreCase));
        }

        public string SomeProperty { get; set; } = "Test";
    }

#if NO_XUNIT
    [AttributeUsage(AttributeTargets.Method)]
    class FactAttribute : Attribute
    {
    }

    static class Assert
    {
        public static void Main()
        {
            Console.WriteLine("-!- Running Test -!-");

            new Integration().Test();

            Console.WriteLine("-!- Test Succeeded -!-");
        }

        public static void Equal<T>(T x, T y) where T : IEquatable<T>
        {
            if (Equals(x, y))
                return;

            Console.WriteLine($"Expected: {x}");
            Console.WriteLine($"Actual:   {y}");
            Environment.Exit(1);
        }

        public static void Empty(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            Console.WriteLine("Expected: Empty String");
            Console.WriteLine($"Actual:   {value}");
            Environment.Exit(1);
        }

        public static void Contains(string[] values, Func<string, bool> predicate)
        {
            if (values.Any(v => predicate(v)))
                return;

            Console.WriteLine($"None of the values {string.Join(", ", values)} matches the predicate");
            Environment.Exit(1);
        }

        public static void DoesNotContain(string[] values, Func<string, bool> predicate)
        {
            if (values.All(v => !predicate(v)))
                return;

            Console.WriteLine($"Any of the values {string.Join(", ", values)} matches the predicate");
            Environment.Exit(1);
        }
    }
#endif
}
