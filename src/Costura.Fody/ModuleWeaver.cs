using System.Collections.Generic;
using System.Diagnostics;
using Fody;

public sealed partial class ModuleWeaver : BaseModuleWeaver
{
    public override void Execute()
    {
//#if DEBUG
//        if (!Debugger.IsAttached)
//        {
//            Debugger.Launch();
//        }
//#endif

        var config = new Configuration(Config);

        WriteInfo($"Costura.Fody v{GetType().Assembly.GetVersion()}");

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
