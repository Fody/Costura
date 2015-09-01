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
                string ext, name;
                GetNameAndExt(parts, out name, out ext);
                return name;
            }, (s, r) => r)
            .Union(ModuleDefinition.Resources.OrderBy(r => r.Name))
            .Where(r => r.Name.StartsWith("costura"))
            .Select(r => r.Name);

        foreach (var resource in orderedResources)
        {
            var parts = resource.Split('.');

            string ext, name;
            GetNameAndExt(parts, out name, out ext);

            if (parts[0] == "costura")
            {
                if (createTemporaryAssemblies)
                    AddToList(preloadListField, resource);
                else
                {
                    if (ext == "pdb")
                        AddToDictionary(symbolNamesField, name, resource);
                    else
                        AddToDictionary(assemblyNamesField, name, resource);
                }
            }
            else if (parts[0] == "costura32")
            {
                AddToList(preload32ListField, resource);
            }
            else if (parts[0] == "costura64")
            {
                AddToList(preload64ListField, resource);
            }
        }
    }

    private static void GetNameAndExt(string[] parts, out string name, out string ext)
    {
        var isZip = parts[parts.Length - 1] == "zip";

        ext = parts[parts.Length - (isZip ? 2 : 1)];

        name = string.Join(".", parts.Skip(1).Take(parts.Length - (isZip ? 3 : 2)));
    }

    void AddToDictionary(FieldDefinition field, string key, string name)
    {
        var retIndex = loaderCctor.Body.Instructions.Count - 1;
        loaderCctor.Body.Instructions.InsertBefore(retIndex, new Instruction[] {
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, key),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, dictionaryOfStringOfStringAdd),
        });
    }

    void AddToList(FieldDefinition field, string name)
    {
        var retIndex = loaderCctor.Body.Instructions.Count - 1;
        loaderCctor.Body.Instructions.InsertBefore(retIndex, new Instruction[] {
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, listOfStringAdd),
        });
    }
}