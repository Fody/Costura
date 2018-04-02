using System;

partial class ModuleWeaver
{
    void FixResourceCase()
    {
        foreach (var resource in ModuleDefinition.Resources)
        {
            if (resource.Name.StartsWith("costura.",StringComparison.OrdinalIgnoreCase))
            {
                resource.Name = resource.Name.ToLowerInvariant();
            }
        }
    }
}