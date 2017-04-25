using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;

partial class ModuleWeaver
{
    readonly ConstructorInfo instructionConstructorInfo = typeof(Instruction).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(OpCode), typeof(object) }, null);
    TypeDefinition targetType;
    TypeDefinition sourceType;
    TypeDefinition commonType;
    MethodDefinition attachMethod;
    MethodDefinition loaderCctor;
    bool hasUnmanaged;
    FieldDefinition assemblyNamesField;
    FieldDefinition symbolNamesField;
    FieldDefinition preloadListField;
    FieldDefinition preload32ListField;
    FieldDefinition preload64ListField;
    FieldDefinition checksumsField;

    void ImportAssemblyLoader(bool createTemporaryAssemblies)
    {
        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = AssemblyResolver,
            ReadSymbols = true,
            SymbolReaderProvider = new PdbReaderProvider(),
            SymbolStream = GetType().Assembly.GetManifestResourceStream("Costura.bin.Template.pdb"),
        };

        using (var resourceStream = GetType().Assembly.GetManifestResourceStream("Costura.bin.Template.dll"))
        {
            var moduleDefinition = ModuleDefinition.ReadModule(resourceStream, readerParameters);

            if (createTemporaryAssemblies)
            {
                sourceType = moduleDefinition.Types.First(x => x.Name == "ILTemplateWithTempAssembly");
                DumpSource("ILTemplateWithTempAssembly");
            }
            else if (hasUnmanaged)
            {
                sourceType = moduleDefinition.Types.First(x => x.Name == "ILTemplateWithUnmanagedHandler");
                DumpSource("ILTemplateWithUnmanagedHandler");
            }
            else
            {
                sourceType = moduleDefinition.Types.First(x => x.Name == "ILTemplate");
                DumpSource("ILTemplate");
            }
            commonType = moduleDefinition.Types.First(x => x.Name == "Common");
            DumpSource("Common");

            targetType = new TypeDefinition("Costura", "AssemblyLoader", sourceType.Attributes, Resolve(sourceType.BaseType));
            targetType.CustomAttributes.Add(new CustomAttribute(compilerGeneratedAttributeCtor));
            ModuleDefinition.Types.Add(targetType);
            CopyFields(sourceType);
            CopyMethod(sourceType.Methods.First(x => x.Name == "ResolveAssembly"));

            loaderCctor = CopyMethod(sourceType.Methods.First(x => x.IsConstructor && x.IsStatic));
            attachMethod = CopyMethod(sourceType.Methods.First(x => x.Name == "Attach"));
        }
    }

    void DumpSource(string file)
    {
        string localFile = Path.Combine(Path.GetDirectoryName(AssemblyFilePath), file + ".cs");

        if (File.Exists(localFile))
            return;

        using (var stream = GetType().Assembly.GetManifestResourceStream(String.Format("Costura.src.{0}.cs", file)))
        {
            using (var outStream = new FileStream(localFile, FileMode.Create))
                stream.CopyTo(outStream);
        }
    }

    void CopyFields(TypeDefinition source)
    {
        foreach (var field in source.Fields)
        {
            var newField = new FieldDefinition(field.Name, field.Attributes, Resolve(field.FieldType));
            targetType.Fields.Add(newField);
            if (field.Name == "assemblyNames")
                assemblyNamesField = newField;
            if (field.Name == "symbolNames")
                symbolNamesField = newField;
            if (field.Name == "preloadList")
                preloadListField = newField;
            if (field.Name == "preload32List")
                preload32ListField = newField;
            if (field.Name == "preload64List")
                preload64ListField = newField;
            if (field.Name == "checksums")
                checksumsField = newField;
        }
    }

    TypeReference Resolve(TypeReference baseType)
    {
        var typeDefinition = baseType.Resolve();
        var typeReference = ModuleDefinition.ImportReference(typeDefinition);
        if (baseType is ArrayType)
        {
            return new ArrayType(typeReference);
        }
        if (baseType.IsGenericInstance)
        {
            typeReference = typeReference.MakeGenericInstanceType(baseType.GetGenericInstanceArguments().ToArray());
        }
        return typeReference;
    }

    MethodDefinition CopyMethod(MethodDefinition templateMethod, bool makePrivate = false)
    {
        var attributes = templateMethod.Attributes;
        if (makePrivate)
        {
            attributes &= ~Mono.Cecil.MethodAttributes.Public;
            attributes |= Mono.Cecil.MethodAttributes.Private;
        }
        var returnType = Resolve(templateMethod.ReturnType);
        var newMethod = new MethodDefinition(templateMethod.Name, attributes, returnType)
        {
            IsPInvokeImpl = templateMethod.IsPInvokeImpl,
            IsPreserveSig = templateMethod.IsPreserveSig,
        };
        if (templateMethod.IsPInvokeImpl)
        {
            var moduleRef = ModuleDefinition.ModuleReferences.FirstOrDefault(mr => mr.Name == templateMethod.PInvokeInfo.Module.Name);
            if (moduleRef == null)
            {
                moduleRef = new ModuleReference(templateMethod.PInvokeInfo.Module.Name);
                ModuleDefinition.ModuleReferences.Add(moduleRef);
            }
            newMethod.PInvokeInfo = new PInvokeInfo(templateMethod.PInvokeInfo.Attributes, templateMethod.PInvokeInfo.EntryPoint, moduleRef);
        }

        if (templateMethod.Body != null)
        {
            newMethod.Body.InitLocals = templateMethod.Body.InitLocals;
            foreach (var variableDefinition in templateMethod.Body.Variables)
            {
                var newVariableDefinition = new VariableDefinition(Resolve(variableDefinition.VariableType));
                //newVariableDefinition.Name = variableDefinition.Name;
                newMethod.Body.Variables.Add(newVariableDefinition);
            }
            CopyInstructions(templateMethod, newMethod);
            CopyExceptionHandlers(templateMethod, newMethod);
        }
        foreach (var parameterDefinition in templateMethod.Parameters)
        {
            var newParameterDefinition = new ParameterDefinition(Resolve(parameterDefinition.ParameterType));
            newParameterDefinition.Name = parameterDefinition.Name;
            newMethod.Parameters.Add(newParameterDefinition);
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
            var newInstruction = CloneInstruction(instruction);
            newMethod.Body.Instructions.Add(newInstruction);
            var sequencePoint = templateMethod.DebugInformation.GetSequencePoint(instruction);
            if (sequencePoint != null)
                newMethod.DebugInformation.SequencePoints.Add(TranslateSequencePoint(newInstruction, sequencePoint));
        }
    }

    Instruction CloneInstruction(Instruction instruction)
    {
        Instruction newInstruction;
        if (instruction.OpCode == OpCodes.Ldstr && ((string)instruction.Operand) == "To be replaced at compile time")
        {
            newInstruction = Instruction.Create(OpCodes.Ldstr, resourcesHash);
        }
        else
        {
            newInstruction = (Instruction)instructionConstructorInfo.Invoke(new[] { instruction.OpCode, instruction.Operand });
            newInstruction.Operand = Import(instruction.Operand);
        }
        //newInstruction.SequencePoint = TranslateSequencePoint(instruction.SequencePoint);
        return newInstruction;
    }

    SequencePoint TranslateSequencePoint(Instruction instruction, SequencePoint sequencePoint)
    {
        if (sequencePoint == null)
            return null;

        var document = new Document(Path.Combine(Path.GetDirectoryName(AssemblyFilePath), Path.GetFileName(sequencePoint.Document.Url)))
        {
            Language = sequencePoint.Document.Language,
            LanguageVendor = sequencePoint.Document.LanguageVendor,
            Type = sequencePoint.Document.Type,
        };

        return new SequencePoint(instruction, document)
        {
            StartLine = sequencePoint.StartLine,
            StartColumn = sequencePoint.StartColumn,
            EndLine = sequencePoint.EndLine,
            EndColumn = sequencePoint.EndColumn,
        };
    }

    object Import(object operand)
    {
        var reference = operand as MethodReference;
        if (reference != null)
        {
            var methodReference = reference;
            if (methodReference.DeclaringType == sourceType || methodReference.DeclaringType == commonType)
            {
                var mr = targetType.Methods.FirstOrDefault(x => x.Name == methodReference.Name && x.Parameters.Count == methodReference.Parameters.Count);
                if (mr == null)
                {
                    //little poetic license... :). .Resolve() doesn't work with "extern" methods
                    return CopyMethod(methodReference.DeclaringType.Resolve().Methods
                                      .First(m => m.Name == methodReference.Name && m.Parameters.Count == methodReference.Parameters.Count),
                        methodReference.DeclaringType != sourceType);
                }
                return mr;
            }
            if (methodReference.DeclaringType.IsGenericInstance)
            {
                return ModuleDefinition.ImportReference(methodReference.Resolve())
                    .MakeHostInstanceGeneric(methodReference.DeclaringType.GetGenericInstanceArguments().ToArray());
            }
            return ModuleDefinition.ImportReference(methodReference.Resolve());
        }
        var typeReference = operand as TypeReference;
        if (typeReference != null)
        {
            return Resolve(typeReference);
        }
        var fieldReference = operand as FieldReference;
        if (fieldReference != null)
        {
            var targetField = targetType.Fields.FirstOrDefault(f => f.Name == fieldReference.Name);
            return targetField;
        }
        return operand;
    }
}