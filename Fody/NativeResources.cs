using System;
using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver
{
    void ProcessNativeResources()
    {
        var moduleName = ModuleDefinition.Assembly.Name.Name;

        foreach (var resource in ModuleDefinition.Resources.OfType<EmbeddedResource>())
        {
            if (resource.Name.StartsWith(moduleName + ".costura"))
            {
                resource.Name = resource.Name.Substring(moduleName.Length + 1).ToLowerInvariant();
                hasUnmanaged = true;
                checksums.Add(resource.Name, CalculateChecksum(resource.GetResourceStream()));
            }
        }
    }
}