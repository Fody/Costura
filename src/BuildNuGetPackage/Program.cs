using System;
using System.Collections.Generic;
using System.IO;
using NuGet;

namespace BuildNuGetPackage
{
    class Program
    {
        class PropertyProvider : Dictionary<string, dynamic>, IPropertyProvider
        {
            dynamic IPropertyProvider.GetPropertyValue(string propertyName)
            {
                dynamic value;
                if (TryGetValue(propertyName, out value))
                    return value;

                return null;
            }
        }

        static void Main(string[] args)
        {
            var path = "";

            if (args.Length == 1)
            {
                path = Path.GetFullPath(args[0]);

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }

            Environment.CurrentDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);

            var propertyProvider = new PropertyProvider()
            {
                { "version", GitVersionInformation.NuGetVersionV2 }
            };

            var packageBuilder = new PackageBuilder();

            using (var spec = File.OpenRead(Path.Combine("Assets", "Costura.Fody.nuspec")))
            {
                var manifest = Manifest.ReadFrom(spec, propertyProvider, false);
                packageBuilder.Populate(manifest.Metadata);
            }

            packageBuilder.PopulateFiles("", new[] {
                new ManifestFile { Source = "Costura.Fody.dll", Target = "" },
                new ManifestFile { Source = "Costura.Fody.pdb", Target = "" }
            });

            packageBuilder.PopulateFiles("Assets", new[] {
                new ManifestFile { Source = "install.ps1", Target = "tools" },
                new ManifestFile { Source = "uninstall.ps1", Target = "tools" }
            });

            var packagePath = Path.Combine(path, packageBuilder.GetFullName() + ".nupkg");

            using (var file = new FileStream(packagePath, FileMode.Create))
            {
                Console.WriteLine($"Saving file {packagePath}");

                packageBuilder.Save(file);
            }
        }
    }
}