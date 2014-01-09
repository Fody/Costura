using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Mono.Cecil;

public partial class ModuleWeaver
{
    public XElement Config { get; set; }
    public Action<string> LogInfo { get; set; }
    public ModuleDefinition ModuleDefinition { get; set; }
    public List<string> ReferenceCopyLocalPaths { get; set; }
    public IAssemblyResolver AssemblyResolver { get; set; }
    public string AssemblyFilePath { get; set; }

    public ModuleWeaver()
    {
        LogInfo = s => { };
    }

    public void Execute()
    {
        var intermediateOutputPath = Path.GetDirectoryName(AssemblyFilePath);
        LogInfo("IntermediateOutputPath resolved to :" + intermediateOutputPath);

        var config = new Configuration(Config);

        FindMsCoreReferences();

        FixResourceCase();
        ProcessNativeResources();
        EmbedResources(config);

        CalculateHash();
        ImportAssemblyLoader(config.CreateTemporaryAssemblies);
        ImportModuleLoader();

        AddChecksumsToTemplate();
        BuildUpNameDictionary(config.CreateTemporaryAssemblies, config.PreloadOrder);
    }
}