using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

partial class ModuleWeaver
{
    void CallAttach()
    {
        if (FindInitializeCalls())
            return;

        AddModuleInitializerCall();
    }

    bool FindInitializeCalls()
    {
        bool found = false;

        foreach (var type in ModuleDefinition.Types)
        {
            if (!type.HasMethods)
                continue;

            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;

                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    if (method.Body.Instructions[i].Operand is MethodReference callMethod && callMethod.FullName == "System.Void CosturaUtility::Initialize()")
                    {
                        found = true;
                        method.Body.Instructions[i] = Instruction.Create(OpCodes.Call, attachMethod);
                    }
                }
            }
        }

        return found;
    }

    void AddModuleInitializerCall()
    {
        const MethodAttributes attributes = MethodAttributes.Private
                                            | MethodAttributes.HideBySig
                                            | MethodAttributes.Static
                                            | MethodAttributes.SpecialName
                                            | MethodAttributes.RTSpecialName;

        var moduleClass = ModuleDefinition.Types.FirstOrDefault(x => x.Name == "<Module>");
        if (moduleClass == null)
        {
            throw new WeavingException("Found no module class!");
        }
        var cctor = moduleClass.Methods.FirstOrDefault(x => x.Name == ".cctor");
        if (cctor == null)
        {
            cctor = new MethodDefinition(".cctor", attributes, voidTypeReference);
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            moduleClass.Methods.Add(cctor);
        }
        cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, attachMethod));
    }
}