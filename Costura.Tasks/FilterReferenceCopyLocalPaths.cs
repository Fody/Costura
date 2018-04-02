using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Costura.Tasks
{
    public class FilterReferenceCopyLocalPaths : Task
    {
        [Required]
        public ITaskItem[] ReferenceCopyLocalPaths { get; set; }

        [Required]
        public ITaskItem[] References { get; set; }

        [Required]
        public string SolutionDir { get; set; }

        [Required]
        public string ProjectDirectory { get; set; }

        [Output]
        public ITaskItem[] FilteredReferenceCopyLocalPaths { get; set; }

        public override bool Execute()
        {
            try
            {
                var configFiles = ConfigFileFinder.FindWeaverConfigs(SolutionDir, ProjectDirectory);
                XElement configXml = null;

                foreach (var configFile in configFiles)
                {
                    var xDocument = GetDocument(configFile);
                    foreach (var element in xDocument.Root.Elements())
                    {
                        var assemblyName = element.Name.LocalName;
                        if (assemblyName == "Costura")
                        {
                            configXml = element;
                        }
                    }
                }

                var config = configXml == null ? null : new Configuration(configXml);

                if (config == null || config.DisableCleanup)
                {
                    FilteredReferenceCopyLocalPaths = ReferenceCopyLocalPaths;
                    return true;
                }

                var filtered = new List<ITaskItem>();

                if (config.IncludeAssemblies.Any())
                {
                    foreach (var file in ReferenceCopyLocalPaths)
                    {
                        var assemblyName = Path.GetFileNameWithoutExtension(file.ItemSpec);

                        if (!config.IncludeAssemblies.Contains(assemblyName) &&
                            !config.Unmanaged32Assemblies.Contains(assemblyName) &&
                            !config.Unmanaged64Assemblies.Contains(assemblyName))
                        {
                            filtered.Add(file);
                        }
                    }
                }
                else if (config.ExcludeAssemblies.Any())
                {
                    foreach (var file in ReferenceCopyLocalPaths)
                    {
                        var assemblyName = Path.GetFileNameWithoutExtension(file.ItemSpec);

                        if (config.ExcludeAssemblies.Contains(assemblyName) ||
                            config.Unmanaged32Assemblies.Contains(assemblyName) ||
                            config.Unmanaged64Assemblies.Contains(assemblyName))
                        {
                            filtered.Add(file);
                        }
                    }
                }
                else if (!config.OptOut)
                {
                    foreach (var file in ReferenceCopyLocalPaths)
                    {
                        var assemblyName = Path.GetFileNameWithoutExtension(file.ItemSpec);

                        if (config.Unmanaged32Assemblies.All(x => x != assemblyName) &&
                            config.Unmanaged64Assemblies.All(x => x != assemblyName))
                        {
                            filtered.Add(file);
                        }
                    }
                }

                FilteredReferenceCopyLocalPaths = filtered.ToArray();

                return true;
            }
            catch
            {
                return false;
            }
        }

        static XDocument GetDocument(string configFilePath)
        {
            try
            {
                return XDocument.Load(configFilePath);
            }
            catch (XmlException exception)
            {
                throw new WeavingException($"Could not read '{"FodyWeavers.xml"}' because it has invalid xml. Message: '{exception.Message}'.");
            }
        }
    }
}