using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

partial class ModuleWeaver
{
    void BuildUpNameDictionary(bool createTemporaryAssemblies, List<string> preloadOrder)
    {
        var orderedResources = preloadOrder
            .Join(ModuleDefinition.Resources, p => p.ToLowerInvariant(),
            r =>
            {
                var parts = r.Name.Split('.');
                GetNameAndExt(parts, out var name, out _);
                return name;
            }, (s, r) => r)
            .Union(ModuleDefinition.Resources.OrderBy(r => r.Name))
            .Where(r => r.Name.StartsWith("costura", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Name);

        foreach (var resource in orderedResources)
        {
            var parts = resource.Split('.');

            GetNameAndExt(parts, out var name, out var ext);

            if (string.Equals(parts[0], "costura", StringComparison.OrdinalIgnoreCase))
            {
                if (createTemporaryAssemblies)
                {
                    AddToList(preloadListField, resource);
                }
                else
                {
                    if (string.Equals(ext, "pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        AddToDictionary(symbolNamesField, name, resource);
                    }
                    else
                    {
                        AddToDictionary(assemblyNamesField, name, resource);
                    }
                }
            }
            else if (string.Equals(parts[0], "costura32", StringComparison.OrdinalIgnoreCase))
            {
                AddToList(preload32ListField, resource);
            }
            else if (string.Equals(parts[0], "costura64", StringComparison.OrdinalIgnoreCase))
            {
                AddToList(preload64ListField, resource);
            }
        }
    }

    static void GetNameAndExt(string[] parts, out string name, out string ext)
    {
        var isCompressed = string.Equals(parts[parts.Length - 1], "compressed", StringComparison.OrdinalIgnoreCase);

        ext = parts[parts.Length - (isCompressed ? 2 : 1)];

        name = string.Join(".", parts.Skip(1).Take(parts.Length - (isCompressed ? 3 : 2)));
    }

    void AddToDictionary(FieldDefinition field, string key, string name)
    {
        var retIndex = loaderCctor.Body.Instructions.Count - 1;
        loaderCctor.Body.Instructions.InsertBefore(retIndex,
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, key),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, dictionaryOfStringOfStringAdd));
    }

    void AddToList(FieldDefinition field, string name)
    {
        var retIndex = loaderCctor.Body.Instructions.Count - 1;
        loaderCctor.Body.Instructions.InsertBefore(retIndex,
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, listOfStringAdd));
    }
}