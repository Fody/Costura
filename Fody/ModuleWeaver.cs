using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

public partial class ModuleWeaver
{
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
        ReadConfig();
        FindMsCoreReferences();

        FixResourceCase();
        ProcessNativeResources();
        EmbedResources();

        CalculateHash();
        ImportAssemblyLoader();
        ImportModuleLoader();

        AddChecksumsToTemplate();
        BuildUpNameDictionary();
    }
}
