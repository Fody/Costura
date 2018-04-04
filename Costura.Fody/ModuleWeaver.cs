using System.Collections.Generic;
using Fody;
using Mono.Cecil;

public partial class ModuleWeaver: BaseModuleWeaver
{
    public IAssemblyResolver AssemblyResolver { get; set; }

    public override void Execute()
    {
        var config = new Configuration(Config);

        FindMsCoreReferences();

        FixResourceCase();
        ProcessNativeResources(!config.DisableCompression);
        EmbedResources(config);

        CalculateHash();
        ImportAssemblyLoader(config.CreateTemporaryAssemblies);
        CallAttach(config);

        AddChecksumsToTemplate();
        BuildUpNameDictionary(config.CreateTemporaryAssemblies, config.PreloadOrder);
    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        yield return "mscorlib";
        yield return "System";
    }

    public override bool ShouldCleanReference => true;
}