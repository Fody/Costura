using System;

public partial class ModuleWeaver
{
    private void FixResourceCase()
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