using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

partial class ModuleWeaver
{
    ConstructorInfo instructionConstructorInfo = typeof(Instruction).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(OpCode), typeof(object) }, null);
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
        var localFile = Path.Combine(Path.GetDirectoryName(AssemblyFilePath), file + ".cs");

        if (File.Exists(localFile))
            return;

        using (var stream = GetType().Assembly.GetManifestResourceStream(String.Format("Costura.Fody.template.{0}.cs", file)))
        using (var outStream = new FileStream(localFile, FileMode.Create))
        {
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
                newNestedType.BaseType = Resolve(source.BaseType);
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
        var typeDefinitionName = GetTargetTypeName(baseType);
        var typeDefinition = FindType(typeDefinitionName);
        if (typeDefinition == null)
        {
            typeDefinition = baseType.Resolve();
        }

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
        if (operand == null)
        {
            return null;
        }

        var reference = operand as MethodReference;
        if (reference != null)
        {
            var methodReference = reference;

            var declaringTypeName = GetTargetTypeName(methodReference.DeclaringType);
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

            var originalMethodReference = methodReference;
            if (IsWrongMsCoreScope(methodReference.DeclaringType))
            {
                methodReference = (MethodReference)ResolveMsCoreReference(methodReference);
            }

            if (methodReference.DeclaringType.HasGenericParameters || methodReference.DeclaringType.IsGenericInstance)
            {
                // Always resolve type generics
                var typeGenerics = originalMethodReference.DeclaringType.GetGenericInstanceArguments().Select(genericArgument => (TypeReference)ResolveMsCoreReference(genericArgument)).ToList();
                methodReference = methodReference.MakeHostInstanceGeneric(typeGenerics.ToArray());

                // If this method is generic, it could have different generic arguments
                if (methodReference.HasGenericParameters)
                {
                    var methodGenerics = originalMethodReference.DeclaringType.GetGenericInstanceArguments().Select(genericArgument => (TypeReference)ResolveMsCoreReference(genericArgument)).ToList();
                    methodReference = methodReference.MakeHostInstanceGeneric(methodGenerics.ToArray());
                }

                var importedMethodReference = ModuleDefinition.ImportReference(methodReference);
                return importedMethodReference;
            }

            var importedReference = ModuleDefinition.ImportReference(methodReference.Resolve());
            return importedReference;
        }

        var typeReference = operand as TypeReference;
        if (typeReference != null)
        {
            if (IsWrongMsCoreScope(typeReference))
            {
                typeReference = (TypeReference)ResolveMsCoreReference(typeReference);
            }

            var referencedTypeName = GetTargetTypeName(typeReference);
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

            if (IsWrongMsCoreScope(fieldReference.DeclaringType))
            {
                fieldReference = (FieldReference)ResolveMsCoreReference(fieldReference);
            }

            var declaringTypeName = GetTargetTypeName(fieldReference.DeclaringType);
            var targetDeclaringType = FindType(declaringTypeName);
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
            if (IsTemplateType(sourceType))
            {
                // Never include ILTemplate classes, we need to remove that
                continue;
            }

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

    string GetTargetTypeName(TypeReference sourceType)
    {
        var sourceTypeName = sourceType.FullName;
        var targetTypeName = sourceTypeName;

        // Special check so we don't screw up other assemblies
        if (IsTemplateType(sourceType))
        {
            targetTypeName = sourceTypeName.Replace("ILTemplateWithTempAssembly", "Costura.AssemblyLoader")
                .Replace("ILTemplateWithUnmanagedHandler", "Costura.AssemblyLoader")
                .Replace("ILTemplate", "Costura.AssemblyLoader")
                .Replace("Common", "Costura.AssemblyLoader");
        }

        return targetTypeName;
    }

    bool IsTemplateType(TypeReference type)
    {
        var module = type.Module;
        if (module == null)
        {
            return false;
        }

        return module.Name == "Template.dll";
    }

    private bool IsWrongMsCoreScope(TypeReference type)
    {
        var actualScope = type.Scope as AssemblyNameReference;
        if ((actualScope != null) && (actualScope.Name == "mscorlib"))
        {
            var expectedScope = msCoreTypes.First().Scope as ModuleDefinition;
            if (expectedScope != null)
            {
                if (expectedScope.Assembly.Name.Version != actualScope.Version)
                {
                    return true;
                }
            }
        }

        return false;
    }

    object ResolveMsCoreReference(object reference)
    {
        var typeReference = reference as TypeReference;
        if (typeReference != null)
        {
            return GetMsCoreType(typeReference);
        }

        var methodReference = reference as MethodReference;
        if (methodReference != null)
        {
            var declaringType = GetMsCoreType(methodReference.DeclaringType).Resolve();

            var possibleMembers = declaringType.Methods.Where(x => string.Equals(methodReference.Name, x.Name) &&
                methodReference.Parameters.Count == x.Parameters.Count).Select(x => x.Resolve());

            foreach (var possibleMember in possibleMembers)
            {
                var isValid = true;

                var parameters = methodReference.Parameters;
                var possibleParameters = possibleMember.Parameters;

                for (var i = 0; i < methodReference.Parameters.Count; i++)
                {
                    // Generic parameter names might not be comparable, so only compare names if not generic
                    if (parameters[i].ParameterType.IsGenericParameter)
                    {
                        if (!possibleParameters[i].ParameterType.IsGenericParameter)
                        {
                            isValid = false;
                            break;
                        }
                    }
                    else if (parameters[i].ParameterType.IsByReference)
                    {
                        if (!possibleParameters[i].ParameterType.IsByReference)
                        {
                            isValid = false;
                            break;
                        }
                    }
                    else if (parameters[i].ParameterType.Name != possibleParameters[i].ParameterType.Name)
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    return possibleMember.Resolve();
                }
            }
        }

        var propertyReference = reference as PropertyReference;
        if (propertyReference != null)
        {
            var declaringType = GetMsCoreType(propertyReference.DeclaringType).Resolve();
            return declaringType.Properties.First(x => string.Equals(x.Name, propertyReference.Name)).Resolve();
        }

        var fieldReference = reference as FieldReference;
        if (fieldReference != null)
        {
            var declaringType = GetMsCoreType(fieldReference.DeclaringType).Resolve();
            return declaringType.Fields.First(x => string.Equals(x.Name, fieldReference.Name)).Resolve();
        }

        return reference;
    }

    TypeReference GetMsCoreType(TypeReference declaringType)
    {
        if (IsWrongMsCoreScope(declaringType))
        {
            if (declaringType.IsGenericInstance)
            {
                declaringType = declaringType.Resolve();
            }

            // Always ensure the right version of mscorlib
            var msCoreType = (from type in msCoreTypes
                              where string.Equals(declaringType.FullName, type.FullName)
                              select type).FirstOrDefault();
            if (msCoreType == null)
            {
                LogError(string.Format("Trying to redirect mscorlib type '{0}', but can't find it, please contact the Costura team", declaringType.FullName));
            }
            else
            {
                return msCoreType;
            }
        }

        return declaringType;
    }
}