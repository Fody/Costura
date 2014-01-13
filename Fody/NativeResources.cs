using System;
using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver
{
    void ProcessNativeResources()
    {
        foreach (var resource in ModuleDefinition.Resources.OfType<EmbeddedResource>())
        {
            if (resource.Name.IndexOf(".costura", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                resource.Name = resource.Name.Substring(resource.Name.IndexOf(".costura", StringComparison.OrdinalIgnoreCase) + 1).ToLowerInvariant();
                hasUnmanaged = true;
                checksums.Add(resource.Name, CalculateChecksum(resource.GetResourceStream()));
            }
        }
    }
}