using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public partial class ModuleWeaver
{
    private void BuildUpNameDictionary(bool createTemporaryAssemblies, List<string> preloadOrder)
    {
        var orderedResources = preloadOrder
            .Join(ModuleDefinition.Resources, _ => _.ToLowerInvariant(),
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
            // Skip metadata
            if (resource == "costura.metadata")
            {
                continue;
            }

            var parts = resource.Split('.');

            GetNameAndExt(parts, out var name, out var ext);

            if (string.Equals(parts[0], "costura", StringComparison.OrdinalIgnoreCase))
            {
                if (createTemporaryAssemblies)
                {
                    AddToList(_preloadListField, resource);
                }
                else
                {
                    if (string.Equals(ext, "pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        AddToDictionary(_symbolNamesField, name, resource);
                    }
                    else
                    {
                        AddToDictionary(_assemblyNamesField, name, resource);
                    }
                }
            }
            else if (string.Equals(parts[0], "costura32", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(parts[0], "costura_win_x86", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(parts[0], "costura32", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWarning("It's recommended to use costuraX86 instead of costura32 for native assemblies");
                }

                AddToList(_preloadWinX86ListField, resource);
            }
            else if (string.Equals(parts[0], "costura64", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(parts[0], "costura_win_x64", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(parts[0], "costura64", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWarning("It's recommended to use costuraX64 instead of costura64 for native assemblies");
                }

                AddToList(_preloadWinX64ListField, resource);
            }
            else if (string.Equals(parts[0], "costura_win_arm64", StringComparison.OrdinalIgnoreCase))
            {
                AddToList(_preloadWinArm64ListField, resource);
            }
        }
    }

    private static void GetNameAndExt(string[] parts, out string name, out string ext)
    {
        var isCompressed = string.Equals(parts[parts.Length - 1], "compressed", StringComparison.OrdinalIgnoreCase);

        ext = parts[parts.Length - (isCompressed ? 2 : 1)];

        name = string.Join(".", parts.Skip(1).Take(parts.Length - (isCompressed ? 3 : 2)));
    }

    private void AddToDictionary(FieldDefinition field, string key, string name)
    {
        var retIndex = _loaderCctor.Body.Instructions.Count - 1;
        _loaderCctor.Body.Instructions.InsertBefore(retIndex,
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, key),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, _dictionaryOfStringOfStringAdd));
    }

    private void AddToList(FieldDefinition field, string name)
    {
        var retIndex = _loaderCctor.Body.Instructions.Count - 1;
        _loaderCctor.Body.Instructions.InsertBefore(retIndex,
            Instruction.Create(OpCodes.Ldsfld, field),
            Instruction.Create(OpCodes.Ldstr, name),
            Instruction.Create(OpCodes.Callvirt, _listOfStringAdd));
    }
}
