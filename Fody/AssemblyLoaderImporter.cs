using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

public partial class ModuleWeaver
{
    ConstructorInfo instructionConstructorInfo = typeof (Instruction).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] {typeof (OpCode), typeof (object)}, null);
    TypeDefinition targetType;
    TypeDefinition sourceType;
    public MethodDefinition AttachMethod;


    public void ImportAssemblyLoader()
    {
        var existingILTemplate = ModuleDefinition.GetTypes().FirstOrDefault(x => x.FullName == "Costura.AssemblyLoader");
        if (existingILTemplate != null)
        {
            AttachMethod = existingILTemplate.Methods.First(x => x.Name == "Attach");
            return;
        }


        var moduleDefinition = GetTemplateModuleDefinition();

        if (CreateTemporaryAssemblies)
        {
            sourceType = moduleDefinition.Types.First(x => x.Name == "ILTemplateWithTempAssembly");
        }
        else
        {
            sourceType = moduleDefinition.Types.First(x => x.Name == "ILTemplate");
        }

        targetType = new TypeDefinition("Costura", "AssemblyLoader", sourceType.Attributes, Resolve(sourceType.BaseType));
        ModuleDefinition.Types.Add(targetType);
        CopyFields(sourceType);
        CopyMethod(sourceType.Methods.First(x => x.Name == "ResolveAssembly"));

        AttachMethod = CopyMethod(sourceType.Methods.First(x => x.Name == "Attach"));
    }

    void CopyFields(TypeDefinition source)
    {
        foreach (var field in source.Fields)
        {
            targetType.Fields.Add(new FieldDefinition(field.Name, field.Attributes, Resolve(field.FieldType)));
        }
    }

    ModuleDefinition GetTemplateModuleDefinition()
    {
        var readerParameters = new ReaderParameters
                                   {
                                       AssemblyResolver = AssemblyResolver,
                                   };

        using (var resourceStream = GetType().Assembly.GetManifestResourceStream("Costura.Fody.Template.dll"))
        {
            return ModuleDefinition.ReadModule(resourceStream, readerParameters);
        }
    }


    TypeReference Resolve(TypeReference baseType)
    {
        var typeDefinition = baseType.Resolve();
        var typeReference = ModuleDefinition.Import(typeDefinition);
        if (baseType is ArrayType)
        {
            return new ArrayType(typeReference);
        }
        return typeReference;
    }


    MethodDefinition CopyMethod(MethodDefinition templateMethod)
    {
        var returnType = Resolve(templateMethod.ReturnType);
        var newMethod = new MethodDefinition(templateMethod.Name, templateMethod.Attributes, returnType)
                            {
                                IsPInvokeImpl = templateMethod.IsPInvokeImpl,
                                IsPreserveSig = templateMethod.IsPreserveSig,
                            };
        if (templateMethod.IsPInvokeImpl)
        {
            var moduleRef = new ModuleReference(templateMethod.PInvokeInfo.Module.Name);
            ModuleDefinition.ModuleReferences.Add(moduleRef);
            newMethod.PInvokeInfo = new PInvokeInfo(templateMethod.PInvokeInfo.Attributes, templateMethod.PInvokeInfo.EntryPoint, moduleRef);
        }


        if (templateMethod.Body != null)
        {
            newMethod.Body.InitLocals = templateMethod.Body.InitLocals;
            foreach (var variableDefinition in templateMethod.Body.Variables)
            {
                newMethod.Body.Variables.Add(new VariableDefinition(Resolve(variableDefinition.VariableType)));
            }
            CopyInstructions(templateMethod, newMethod);
            CopyExceptionHandlers(templateMethod, newMethod);

        }
        foreach (var parameterDefinition in templateMethod.Parameters)
        {
            newMethod.Parameters.Add(new ParameterDefinition(Resolve(parameterDefinition.ParameterType)));
        }

        
        targetType.Methods.Add(newMethod);
        return newMethod;
    }

    void CopyExceptionHandlers(MethodDefinition templateMethod, MethodDefinition newMethod)
    {
        if (!templateMethod.Body.HasExceptionHandlers)
        {
            return;
        }
        foreach (var exceptionHandler in templateMethod.Body.ExceptionHandlers)
        {
            var handler = new ExceptionHandler(exceptionHandler.HandlerType);
            var templateInstructions = templateMethod.Body.Instructions;
            var targetInstructions = newMethod.Body.Instructions;
            if (exceptionHandler.TryStart != null)
            {
                handler.TryStart = targetInstructions[templateInstructions.IndexOf(exceptionHandler.TryStart)];
            }
            if (exceptionHandler.TryEnd != null)
            {
                handler.TryEnd = targetInstructions[templateInstructions.IndexOf(exceptionHandler.TryEnd)];
            }
            if (exceptionHandler.HandlerStart != null)
            {
                handler.HandlerStart = targetInstructions[templateInstructions.IndexOf(exceptionHandler.HandlerStart)];
            }
            if (exceptionHandler.HandlerEnd != null)
            {
                handler.HandlerEnd = targetInstructions[templateInstructions.IndexOf(exceptionHandler.HandlerEnd)];
            }
            if (exceptionHandler.FilterStart != null)
            {
                handler.FilterStart = targetInstructions[templateInstructions.IndexOf(exceptionHandler.FilterStart)];
            }
            if (exceptionHandler.CatchType != null)
            {
                handler.CatchType = Resolve(exceptionHandler.CatchType);
            }
            newMethod.Body.ExceptionHandlers.Add(handler);
        }
    }

    void CopyInstructions(MethodDefinition templateMethod, MethodDefinition newMethod)
    {
        foreach (var instruction in templateMethod.Body.Instructions)
        {
            newMethod.Body.Instructions.Add(CloneInstruction(instruction));
        }
    }

    Instruction CloneInstruction(Instruction instruction)
    {
        var newInstruction = (Instruction) instructionConstructorInfo.Invoke(new[] {instruction.OpCode, instruction.Operand});
        newInstruction.Operand = Import(instruction.Operand);
        return newInstruction;
    }

    object Import(object operand)
    {
        var reference = operand as MethodReference;
        if (reference != null)
        {
            var methodReference = reference;
            if (methodReference.DeclaringType == sourceType)
            {
                var mr = targetType.Methods.FirstOrDefault(x => x.Name == methodReference.Name);
                if (mr == null)
                {
                    //little poetic license... :). .Resolve() doesn't work with "extern" methods
                    return CopyMethod(sourceType.Methods.First(m => m.Name == methodReference.Name
                                                                    && m.Parameters.Count == methodReference.Parameters.Count));
                }
                return mr;
            }
            return ModuleDefinition.Import(methodReference.Resolve());
        }
        var typeReference = operand as TypeReference;
        if (typeReference != null)
        {
            return Resolve(typeReference);
        }
        var fieldReference = operand as FieldReference;
        if (fieldReference != null)
        {
            return targetType.Fields.First(f => f.Name == fieldReference.Name);
        }
        return operand;
    }
}