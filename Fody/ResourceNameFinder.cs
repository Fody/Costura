using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

public partial class ModuleWeaver
{
    void BuildUpNameDictionary()
    {
        foreach (var resource in ModuleDefinition.Resources.OrderBy(r => r.Name))
        {
            if (!resource.Name.StartsWith("costura"))
                continue;

            var parts = resource.Name.Split('.');

            var isZip = parts[parts.Length - 1] == "zip";

            var ext = parts[parts.Length - (isZip ? 2 : 1)];

            var name = string.Join(".", parts.Skip(1).Take(parts.Length - (isZip ? 3 : 2)));

            if (parts[0] == "costura")
            {
                if (CreateTemporaryAssemblies)
                    AddToList(PreloadListField, resource.Name);
                else
                {
                    if (ext == "pdb")
                        AddToDictionary(SymbolNamesField, name, resource.Name);
                    else
                        AddToDictionary(AssemblyNamesField, name, resource.Name);
                }
            }
            else if (parts[0] == "costura32")
            {
                AddToList(Preload32ListField, resource.Name);
            }
            else if (parts[0] == "costura64")
            {
                AddToList(Preload64ListField, resource.Name);
            }
        }
    }

    void AddToDictionary(FieldDefinition field, string key, string name)
    {
        var retIndex = assemblyLoaderCCtor.Body.Instructions.Count - 1;
        assemblyLoaderCCtor.Body.Instructions.InsertBefore(retIndex, new Instruction[] { 
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, key),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, DictionaryOfStringOfStringAdd),
        });
    }

    void AddToList(FieldDefinition field, string name)
    {
        var retIndex = assemblyLoaderCCtor.Body.Instructions.Count - 1;
        assemblyLoaderCCtor.Body.Instructions.InsertBefore(retIndex, new Instruction[] { 
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, ListOfStringAdd),
        });
    }
}
