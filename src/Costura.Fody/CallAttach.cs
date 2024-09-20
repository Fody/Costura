using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

public partial class ModuleWeaver
{
    private void CallAttach(Configuration config)
    {
        var initialized = FindInitializeCalls(config);

        if (config.LoadAtModuleInit)
        {
            AddModuleInitializerCall(config);
        }
        else if (!initialized)
        {
            throw new WeavingException("Costura was not initialized. Make sure LoadAtModuleInit=true or call CosturaUtility.Initialize().");
        }
    }

    private bool FindInitializeCalls(Configuration config)
    {
        var found = false;

        foreach (var type in ModuleDefinition.Types)
        {
            if (!type.HasMethods)
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                var instructions = method.Body.Instructions;
                for (var i = 0; i < instructions.Count; i++)
                {
                    var instruction = instructions[i];
                    if (instruction.OpCode != OpCodes.Call)
                    {
                        continue;
                    }

                    if (instruction.Operand is not MethodReference callMethod)
                    {
                        continue;
                    }
                    
                    if (callMethod.FullName == "System.Void CosturaUtility::Initialize()")
                    {
                        found = true;

                        instructions[i] = Instruction.Create(OpCodes.Call, _attachMethod);
                        instructions.Insert(i--, Instruction.Create(config.DisableEventSubscription ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1));
                    }
                }
            }
        }

        return found;
    }

    private void AddModuleInitializerCall(Configuration config)
    {
        const MethodAttributes attributes = MethodAttributes.Private
                                            | MethodAttributes.HideBySig
                                            | MethodAttributes.Static
                                            | MethodAttributes.SpecialName
                                            | MethodAttributes.RTSpecialName;

        var moduleClass = ModuleDefinition.Types.FirstOrDefault(_ => _.Name == "<Module>");
        if (moduleClass is null)
        {
            throw new WeavingException("Found no module class!");
        }

        var cctor = moduleClass.Methods.FirstOrDefault(_ => _.Name == ".cctor");
        if (cctor is null)
        {
            cctor = new MethodDefinition(".cctor", attributes, TypeSystem.VoidReference);
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            moduleClass.Methods.Add(cctor);
        }

        cctor.Body.Instructions.Insert(0, Instruction.Create(config.DisableEventSubscription ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1));
        cctor.Body.Instructions.Insert(1, Instruction.Create(OpCodes.Call, _attachMethod));
    }
}
