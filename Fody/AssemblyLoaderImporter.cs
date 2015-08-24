using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
        var moduleDefinition = GetTemplateModuleDefinition();

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

        // Note: unfortunately the order is very important:
        // 1) The fields (so if a static class is used for lambdas, they are available)
        // 2) The method signatures (not the body)
        // 3) Nested types (the actual static lambda classes)
        // 4) The methods using the fields and nested types
        CopyFields(sourceType, targetType);
        CopyMethod(sourceType.Methods.First(x => x.Name == "ResolveAssembly"), sourceType, targetType, true);
        CopyNestedTypes(sourceType, targetType, true);
        CopyMethod(sourceType.Methods.First(x => x.Name == "ResolveAssembly"), sourceType, targetType, false);
        CopyNestedTypes(sourceType, targetType, false);

        loaderCctor = CopyMethod(sourceType.Methods.First(x => x.IsConstructor && x.IsStatic), sourceType, targetType, false);
        attachMethod = CopyMethod(sourceType.Methods.First(x => x.Name == "Attach"), sourceType, targetType, false);
    }

    void DumpSource(string file)
    {
        string localFile = Path.Combine(Path.GetDirectoryName(AssemblyFilePath), file + ".cs");

        if (File.Exists(localFile))
            return;

        using (var stream = GetType().Assembly.GetManifestResourceStream(String.Format("Costura.Fody.template.{0}.cs", file)))
        {
            using (var outStream = new FileStream(localFile, FileMode.Create))
                stream.CopyTo(outStream);
        }
    }

    void CopyNestedTypes(TypeDefinition source, TypeDefinition target, bool signatureOnly)
    {
        foreach (var nestedType in source.NestedTypes)
        {
            var newNestedType = (from type in target.NestedTypes
                                 where string.Equals(type.Name, nestedType.Name)
                                 select type).FirstOrDefault();

            var isExistingType = (newNestedType != null);
            if (!isExistingType)
            {
                newNestedType = new TypeDefinition(nestedType.Namespace, nestedType.Name, nestedType.Attributes);
                newNestedType.BaseType = ModuleDefinition.ImportReference(source.BaseType);
                newNestedType.IsClass = source.IsClass;
                target.NestedTypes.Add(newNestedType);
            }

            CopyNestedTypes(nestedType, newNestedType, signatureOnly);

            if (!isExistingType)
            {
                CopyFields(nestedType, newNestedType);
            }

            CopyMethods(nestedType, newNestedType, signatureOnly);
        }
    }

    void CopyFields(TypeDefinition source, TypeDefinition target)
    {
        foreach (var field in source.Fields)
        {
            var fieldType = Resolve(field.FieldType);
            if (string.Equals(fieldType.FullName, source.FullName))
            {
                fieldType = Resolve(target);
            }

            var newField = new FieldDefinition(field.Name, field.Attributes, fieldType);
            target.Fields.Add(newField);
            if (field.Name == "assemblyNames")
                assemblyNamesField = newField;
            else if (field.Name == "symbolNames")
                symbolNamesField = newField;
            else if (field.Name == "preloadList")
                preloadListField = newField;
            else if (field.Name == "preload32List")
                preload32ListField = newField;
            else if (field.Name == "preload64List")
                preload64ListField = newField;
            else if (field.Name == "checksums")
                checksumsField = newField;
        }
    }

    void CopyMethods(TypeDefinition source, TypeDefinition target, bool signatureOnly)
    {
        foreach (var method in source.Methods)
        {
            CopyMethod(method, source, target, signatureOnly);
        }
    }

    ModuleDefinition GetTemplateModuleDefinition()
    {
        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = AssemblyResolver,
            ReadSymbols = true,
            SymbolStream = GetType().Assembly.GetManifestResourceStream("Costura.Fody.Template.pdb"),
        };

        using (var resourceStream = GetType().Assembly.GetManifestResourceStream("Costura.Fody.Template.dll"))
        {
            return ModuleDefinition.ReadModule(resourceStream, readerParameters);
        }
    }

    TypeReference Resolve(TypeReference baseType)
    {
        var typeDefinitionName = GetTargetTypeName(baseType.Name);
        var typeDefinition = FindType(typeDefinitionName);
        if (typeDefinition == null)
        {
            typeDefinition = baseType.Resolve();
        }

        var typeReference = ModuleDefinition.Import(typeDefinition);
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

    MethodDefinition CopyMethod(MethodDefinition sourceMethod, TypeDefinition source, TypeDefinition target, bool signatureOnly, bool makePrivate = false)
    {
        var newMethod = (from method in target.Methods
                         where string.Equals(sourceMethod.Name, method.Name)
                         select method).FirstOrDefault();

        var isExistingMethod = (newMethod != null);
        if (!isExistingMethod)
        {
            var attributes = sourceMethod.Attributes;
            if (makePrivate)
            {
                attributes &= ~Mono.Cecil.MethodAttributes.Public;
                attributes |= Mono.Cecil.MethodAttributes.Private;
            }
            var returnType = Resolve(sourceMethod.ReturnType);

            newMethod = new MethodDefinition(sourceMethod.Name, attributes, returnType)
            {
                IsPInvokeImpl = sourceMethod.IsPInvokeImpl,
                IsPreserveSig = sourceMethod.IsPreserveSig,
            };

            if (sourceMethod.IsPInvokeImpl)
            {
                var moduleRef = ModuleDefinition.ModuleReferences.FirstOrDefault(mr => mr.Name == sourceMethod.PInvokeInfo.Module.Name);
                if (moduleRef == null)
                {
                    moduleRef = new ModuleReference(sourceMethod.PInvokeInfo.Module.Name);
                    ModuleDefinition.ModuleReferences.Add(moduleRef);
                }
                newMethod.PInvokeInfo = new PInvokeInfo(sourceMethod.PInvokeInfo.Attributes, sourceMethod.PInvokeInfo.EntryPoint, moduleRef);
            }
        }

        if (!signatureOnly && sourceMethod.Body != null)
        {
            newMethod.Body.InitLocals = sourceMethod.Body.InitLocals;
            foreach (var variableDefinition in sourceMethod.Body.Variables)
            {
                var newVariableDefinition = new VariableDefinition(Resolve(variableDefinition.VariableType));
                newVariableDefinition.Name = variableDefinition.Name;
                newMethod.Body.Variables.Add(newVariableDefinition);
            }
            CopyInstructions(sourceMethod, newMethod, source, target);
            CopyExceptionHandlers(sourceMethod, newMethod);
        }

        if (!isExistingMethod)
        {
            foreach (var parameterDefinition in sourceMethod.Parameters)
            {
                var newParameterDefinition = new ParameterDefinition(Resolve(parameterDefinition.ParameterType));
                newParameterDefinition.Name = parameterDefinition.Name;
                newMethod.Parameters.Add(newParameterDefinition);
            }

            target.Methods.Add(newMethod);
        }

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

    void CopyInstructions(MethodDefinition templateMethod, MethodDefinition newMethod, TypeDefinition source, TypeDefinition target)
    {
        foreach (var instruction in templateMethod.Body.Instructions)
        {
            newMethod.Body.Instructions.Add(CloneInstruction(instruction, source, target));
        }
    }

    Instruction CloneInstruction(Instruction instruction, TypeDefinition source, TypeDefinition target)
    {
        Instruction newInstruction;
        if (instruction.OpCode == OpCodes.Ldstr && ((string)instruction.Operand) == "To be replaced at compile time")
        {
            newInstruction = Instruction.Create(OpCodes.Ldstr, resourcesHash);
        }
        else
        {
            newInstruction = (Instruction)instructionConstructorInfo.Invoke(new[] { instruction.OpCode, instruction.Operand });
            newInstruction.Operand = Import(instruction.Operand, source, target);
        }
        newInstruction.SequencePoint = TranslateSequencePoint(instruction.SequencePoint);
        return newInstruction;
    }

    SequencePoint TranslateSequencePoint(SequencePoint sequencePoint)
    {
        if (sequencePoint == null)
            return null;

        var document = new Document(Path.Combine(Path.GetDirectoryName(AssemblyFilePath), Path.GetFileName(sequencePoint.Document.Url)))
        {
            Language = sequencePoint.Document.Language,
            LanguageVendor = sequencePoint.Document.LanguageVendor,
            Type = sequencePoint.Document.Type,
        };

        return new SequencePoint(document)
        {
            StartLine = sequencePoint.StartLine,
            StartColumn = sequencePoint.StartColumn,
            EndLine = sequencePoint.EndLine,
            EndColumn = sequencePoint.EndColumn,
        };
    }

    object Import(object operand, TypeDefinition source, TypeDefinition target)
    {
        var reference = operand as MethodReference;
        if (reference != null)
        {
            var methodReference = reference;

            var declaringTypeName = GetTargetTypeName(methodReference.DeclaringType.FullName);
            var targetDeclaringType = FindType(declaringTypeName);
            if (targetDeclaringType != null || methodReference.DeclaringType == commonType)
            {
                if (targetDeclaringType == null)
                {
                    // Common type, fallback to target
                    targetDeclaringType = target;
                }

                var mr = targetDeclaringType.Methods.FirstOrDefault(x => x.Name == methodReference.Name && x.Parameters.Count == methodReference.Parameters.Count);
                if (mr == null)
                {
                    //little poetic license... :). .Resolve() doesn't work with "extern" methods
                    return CopyMethod(methodReference.DeclaringType.Resolve().Methods
                                      .First(m => m.Name == methodReference.Name && m.Parameters.Count == methodReference.Parameters.Count),
                                      source, target, false, methodReference.DeclaringType != sourceType);
                }

                return (targetDeclaringType.Module != ModuleDefinition) ? ModuleDefinition.ImportReference(mr.Resolve()) : mr;
            }

            if (methodReference.DeclaringType.IsGenericInstance)
            {
                return ModuleDefinition.Import(methodReference.Resolve())
                    .MakeHostInstanceGeneric(methodReference.DeclaringType.GetGenericInstanceArguments().ToArray());
            }

            return ModuleDefinition.Import(methodReference.Resolve());
        }

        var typeReference = operand as TypeReference;
        if (typeReference != null)
        {
            var referencedTypeName = GetTargetTypeName(typeReference.FullName);
            var targetReferenceType = FindType(referencedTypeName);
            if (targetReferenceType == null)
            {
                targetReferenceType = typeReference.Resolve();
            }

            return Resolve(targetReferenceType);
        }

        var fieldReference = operand as FieldReference;
        if (fieldReference != null)
        {
            var field = target.Fields.FirstOrDefault(f => f.Name == fieldReference.Name);
            if (field != null)
            {
                return field;
            }

            var declaringTypeName = GetTargetTypeName(fieldReference.DeclaringType.FullName);
            var targetDeclaringType = FindType(declaringTypeName);
            if (targetDeclaringType == null || fieldReference.DeclaringType == commonType)
            {
                targetDeclaringType = fieldReference.DeclaringType.Resolve();
            }

            if (targetDeclaringType != null)
            {
                field = targetDeclaringType.Fields.FirstOrDefault(f => f.Name == fieldReference.Name);
                if (field != null)
                {
                    return field;
                }
            }

            LogError(string.Format("Field '{0}' could not be found, probably an error while cloning the IL templates, please report to the Costura team", fieldReference.Name));
            return null;
        }

        return operand;
    }

    TypeDefinition FindType(string fullTypeName, TypeDefinition parentType = null)
    {
        var sourceTypes = (parentType != null) ? parentType.NestedTypes : ModuleDefinition.Types;

        foreach (var sourceType in sourceTypes)
        {
            if (string.Equals(sourceType.FullName, fullTypeName))
            {
                return sourceType;
            }

            var nestedType = FindType(fullTypeName, sourceType);
            if (nestedType != null)
            {
                return nestedType;
            }
        }

        return null;
    }

    string GetTargetTypeName(string sourceTypeName)
    {
        return sourceTypeName.Replace("ILTemplate", "Costura.AssemblyLoader");
    }
}