using System;

public partial class ModuleWeaver
{
    public void FixResourceCase()
    {
        foreach (var resource in ModuleDefinition.Resources)
        {
            if (resource.Name.StartsWith("costura.", StringComparison.InvariantCultureIgnoreCase))
            {
                resource.Name = resource.Name.ToLowerInvariant();
            }
        }
    }
}