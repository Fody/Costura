public partial class ModuleWeaver
{
    public void ProcessNativeResources()
    {
        var moduleName = ModuleDefinition.Name.Replace(".dll", "");

        foreach (var resource in ModuleDefinition.Resources)
        {
            if (resource.Name.StartsWith(moduleName + ".costura"))
            {
                resource.Name = resource.Name.Substring(moduleName.Length + 1).ToLowerInvariant();
            }
        }
    }
}