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
                new ManifestFile { Source = "Costura.Fody.pdb", Target = "" },
                new ManifestFile { Source = "Costura.Tasks.dll", Target = "" },
                new ManifestFile { Source = "Costura.Tasks.pdb", Target = "" }
            });

#if DEBUG
            var config = "Debug";
#else
            var config = "Release";
#endif

            packageBuilder.PopulateFiles("..\\..\\..\\Costura.Lib\\bin\\" + config, new[] {
                new ManifestFile { Source = "Costura.dll", Target = "lib/dotnet" },
                new ManifestFile { Source = "Costura.pdb", Target = "lib/dotnet" },
                new ManifestFile { Source = "Costura.dll", Target = "lib/portable-net+sl+win+wpa+wp" },
                new ManifestFile { Source = "Costura.pdb", Target = "lib/portable-net+sl+win+wpa+wp" }
            });

            // TODO NetStandard not supported by Costura
            //packageBuilder.PopulateFiles($"..\\..\\..\\Costura.LibNetStandard\\bin\\{config}\\netstandard1.4", new[] {
            //    new ManifestFile { Source = "Costura.dll", Target = "lib/netstandard1.4" },
            //    new ManifestFile { Source = "Costura.pdb", Target = "lib/netstandard1.4" }
            //});

            packageBuilder.PopulateFiles("Assets", new[] {
                new ManifestFile { Source = "install.ps1", Target = "tools" },
                new ManifestFile { Source = "uninstall.ps1", Target = "tools" },
                new ManifestFile { Source = "Costura.Fody.targets", Target = "build/dotnet" },
                new ManifestFile { Source = "Costura.Fody.targets", Target = "build/portable-net+sl+win+wpa+wp" }
            });

            var packagePath = Path.Combine(path, $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg");

            using (var file = new FileStream(packagePath, FileMode.Create))
            {
                Console.WriteLine($"Saving file {packagePath}");

                packageBuilder.Save(file);
            }
        }
    }
}