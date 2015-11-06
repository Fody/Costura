partial class ModuleWeaver
{
    void FixResourceCase()
    {
        foreach (var resource in ModuleDefinition.Resources)
        {
            if (resource.Name.StartsWith("costura."))
            {
                resource.Name = resource.Name.ToLowerInvariant();
            }
        }
    }
}