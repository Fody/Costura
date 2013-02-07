using System;
using System.Collections.Generic;
using Mono.Cecil;

public partial  class ModuleWeaver
{
    public Action<string> LogInfo { get; set; }
    public ModuleDefinition ModuleDefinition { get; set; }
    public List<string> ReferenceCopyLocalPaths { get; set; }
    public IAssemblyResolver AssemblyResolver { get; set; }


    public ModuleWeaver()
    {
        LogInfo = s => { };
    }

    public void Execute()
    {
        ReadConfig();
        FindMsCoreReferences();

        ImportAssemblyLoader();
        ImportModuleLoader();
        FixResourceCase();
        EmbedResources();
    }
}