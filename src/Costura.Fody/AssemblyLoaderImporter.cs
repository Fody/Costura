using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;

public partial class ModuleWeaver
{
    private readonly ConstructorInfo _instructionConstructorInfo = typeof(Instruction).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(OpCode), typeof(object) }, null);
    private TypeDefinition _targetType;
    private TypeDefinition _sourceType;
    private TypeDefinition _commonType;
    private MethodDefinition _attachMethod;
    private MethodDefinition _loaderCctor;
    private bool _hasUnmanaged;
    private FieldDefinition _assemblyNamesField;
    private FieldDefinition _symbolNamesField;
    private FieldDefinition _preloadListField;
    private FieldDefinition _preload32ListField;
    private FieldDefinition _preload64ListField;
    private FieldDefinition _checksumsField;

    private void ImportAssemblyLoader(bool createTemporaryAssemblies)
    {
        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = AssemblyResolver,
            ReadSymbols = true,
            SymbolReaderProvider = new PdbReaderProvider()
        };

        using (var resourceStream = GetType().Assembly.GetManifestResourceStream("Costura.Template.dll"))
        {
            var moduleDefinition = ModuleDefinition.ReadModule(resourceStream, readerParameters);

            if (createTemporaryAssemblies)
            {
                _sourceType = moduleDefinition.Types.Single(x => x.Name == "ILTemplateWithTempAssembly");
                DumpSource("ILTemplateWithTempAssembly");
            }
            else if (_hasUnmanaged)
            {
                _sourceType = moduleDefinition.Types.Single(x => x.Name == "ILTemplateWithUnmanagedHandler");
                DumpSource("ILTemplateWithUnmanagedHandler");
            }
            else
            {
                _sourceType = moduleDefinition.Types.Single(x => x.Name == "ILTemplate");
                DumpSource("ILTemplate");
            }
            _commonType = moduleDefinition.Types.Single(x => x.Name == "Common");
            DumpSource("Common");

            _targetType = new TypeDefinition("Costura", "AssemblyLoader", _sourceType.Attributes, Resolve(_sourceType.BaseType));
            _targetType.CustomAttributes.Add(new CustomAttribute(_compilerGeneratedAttributeCtor));
            ModuleDefinition.Types.Add(_targetType);
            CopyFields(_sourceType);
            CopyMethod(_sourceType.Methods.Single(x => x.Name == "ResolveAssembly"));

            _loaderCctor = CopyMethod(_sourceType.Methods.Single(x => x.IsConstructor && x.IsStatic));
            _attachMethod = CopyMethod(_sourceType.Methods.Single(x => x.Name == "Attach"));
        }
    }

    private void DumpSource(string file)
    {
        var localFile = Path.Combine(Path.GetDirectoryName(AssemblyFilePath), file + ".cs");

        if (File.Exists(localFile))
        {
            return;
        }

        var resourceStream = GetType().Assembly.GetManifestResourceStream($"Costura.src.{file}.cs");
        if (resourceStream is not null)
        {
            using (resourceStream)
            using (var outStream = new FileStream(localFile, FileMode.Create))
            {
                resourceStream.CopyTo(outStream);
            }
        }
    }

    private void CopyFields(TypeDefinition source)
    {
        foreach (var field in source.Fields)
        {
            var newField = new FieldDefinition(field.Name, field.Attributes, Resolve(field.FieldType));
            _targetType.Fields.Add(newField);
            if (field.Name == "assemblyNames")
            {
                _assemblyNamesField = newField;
            }

            if (field.Name == "symbolNames")
            {
                _symbolNamesField = newField;
            }

            if (field.Name == "preloadList")
            {
                _preloadListField = newField;
            }
            if (field.Name == "preload32List")
            {
                _preload32ListField = newField;
            }
            if (field.Name == "preload64List")
            {
                _preload64ListField = newField;
            }
            if (field.Name == "checksums")
            {
                _checksumsField = newField;
            }
        }
    }

    private TypeReference Resolve(TypeReference baseType)
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

    private MethodDefinition CopyMethod(MethodDefinition templateMethod, bool makePrivate = false)
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
            if (moduleRef is null)
            {
                moduleRef = new ModuleReference(templateMethod.PInvokeInfo.Module.Name);
                ModuleDefinition.ModuleReferences.Add(moduleRef);
            }
            newMethod.PInvokeInfo = new PInvokeInfo(templateMethod.PInvokeInfo.Attributes, templateMethod.PInvokeInfo.EntryPoint, moduleRef);
        }

        if (templateMethod.Body is not null)
        {
            newMethod.Body.InitLocals = templateMethod.Body.InitLocals;
            foreach (var variableDefinition in templateMethod.Body.Variables)
            {
                var newVariableDefinition = new VariableDefinition(Resolve(variableDefinition.VariableType));
                newMethod.Body.Variables.Add(newVariableDefinition);
            }
            CopyInstructions(templateMethod, newMethod);
            CopyExceptionHandlers(templateMethod, newMethod);
        }
        foreach (var parameterDefinition in templateMethod.Parameters)
        {
            var newParameterDefinition = new ParameterDefinition(Resolve(parameterDefinition.ParameterType))
            {
                Name = parameterDefinition.Name
            };
            newMethod.Parameters.Add(newParameterDefinition);
        }

        _targetType.Methods.Add(newMethod);
        return newMethod;
    }

    private void CopyExceptionHandlers(MethodDefinition templateMethod, MethodDefinition newMethod)
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
            if (exceptionHandler.TryStart is not null)
            {
                handler.TryStart = targetInstructions[templateInstructions.IndexOf(exceptionHandler.TryStart)];
            }
            if (exceptionHandler.TryEnd is not null)
            {
                handler.TryEnd = targetInstructions[templateInstructions.IndexOf(exceptionHandler.TryEnd)];
            }
            if (exceptionHandler.HandlerStart is not null)
            {
                handler.HandlerStart = targetInstructions[templateInstructions.IndexOf(exceptionHandler.HandlerStart)];
            }
            if (exceptionHandler.HandlerEnd is not null)
            {
                handler.HandlerEnd = targetInstructions[templateInstructions.IndexOf(exceptionHandler.HandlerEnd)];
            }
            if (exceptionHandler.FilterStart is not null)
            {
                handler.FilterStart = targetInstructions[templateInstructions.IndexOf(exceptionHandler.FilterStart)];
            }
            if (exceptionHandler.CatchType is not null)
            {
                handler.CatchType = Resolve(exceptionHandler.CatchType);
            }
            newMethod.Body.ExceptionHandlers.Add(handler);
        }
    }

    private void CopyInstructions(MethodDefinition templateMethod, MethodDefinition newMethod)
    {
        var newBody = newMethod.Body;
        var newInstructions = newBody.Instructions;
        var newDebugInformation = newMethod.DebugInformation;

        var templateDebugInformation = templateMethod.DebugInformation;

        foreach (var instruction in templateMethod.Body.Instructions)
        {
            var newInstruction = CloneInstruction(instruction);
            newInstructions.Add(newInstruction);
            var sequencePoint = templateDebugInformation.GetSequencePoint(instruction);
            if (sequencePoint is not null)
            {
                newDebugInformation.SequencePoints.Add(TranslateSequencePoint(newInstruction, sequencePoint));
            }
        }

        var scope = newDebugInformation.Scope = new ScopeDebugInformation(newInstructions.First(), newInstructions.Last());

        foreach (var variable in templateDebugInformation.Scope.Variables)
        {
            var targetVariable = newBody.Variables[variable.Index];

            scope.Variables.Add(new VariableDebugInformation(targetVariable, variable.Name));
        }

    }

    private Instruction CloneInstruction(Instruction instruction)
    {
        if (instruction.OpCode == OpCodes.Ldstr && (string)instruction.Operand == "To be replaced at compile time")
        {
            return Instruction.Create(OpCodes.Ldstr, _resourcesHash);
        }

        var newInstruction = (Instruction)_instructionConstructorInfo.Invoke(new[] { instruction.OpCode, instruction.Operand });
        newInstruction.Operand = Import(instruction.Operand);
        return newInstruction;
    }

    private SequencePoint TranslateSequencePoint(Instruction instruction, SequencePoint sequencePoint)
    {
        if (sequencePoint is null)
        {
            return null;
        }

        return new SequencePoint(instruction, sequencePoint.Document)
        {
            StartLine = sequencePoint.StartLine,
            StartColumn = sequencePoint.StartColumn,
            EndLine = sequencePoint.EndLine,
            EndColumn = sequencePoint.EndColumn,
        };
    }

    private object Import(object operand)
    {
        if (operand is MethodReference reference)
        {
            var methodReference = reference;
            if (methodReference.DeclaringType == _sourceType || methodReference.DeclaringType == _commonType)
            {
                var mr = _targetType.Methods.FirstOrDefault(x => x.Name == methodReference.Name && x.Parameters.Count == methodReference.Parameters.Count);
                if (mr is null)
                {
                    //little poetic license... :). .Resolve() doesn't work with "extern" methods
                    return CopyMethod(methodReference.DeclaringType.Resolve().Methods
                                      .First(m => m.Name == methodReference.Name && m.Parameters.Count == methodReference.Parameters.Count),
                        methodReference.DeclaringType != _sourceType);
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

        if (operand is TypeReference typeReference)
        {
            return Resolve(typeReference);
        }

        if (operand is FieldReference fieldReference)
        {
            return _targetType.Fields.FirstOrDefault(f => f.Name == fieldReference.Name) 
                   ?? new FieldReference(fieldReference.Name, ModuleDefinition.ImportReference(fieldReference.FieldType.Resolve()), ModuleDefinition.ImportReference(fieldReference.DeclaringType.Resolve()));
        }
        return operand;
    }
}
