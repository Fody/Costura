using System;
using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver
{
    void ProcessNativeResources()
    {
        foreach (var resource in ModuleDefinition.Resources.OfType<EmbeddedResource>())
        {
            int costuraHintPosition = resource.Name.IndexOf(".costura", StringComparison.OrdinalIgnoreCase);
            if (costuraHintPosition >= 0)
            {
                resource.Name = resource.Name.Substring(costuraHintPosition + 1).ToLowerInvariant();
                hasUnmanaged = true;
                checksums.Add(resource.Name, CalculateChecksum(resource.GetResourceStream()));
            }
        }
    }
}