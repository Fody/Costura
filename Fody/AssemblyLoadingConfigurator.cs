using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

partial class ModuleWeaver
{
    void SetBoolean(FieldDefinition field, bool value)
    {
        var retIndex = loaderCctor.Body.Instructions.Count - 1;
        loaderCctor.Body.Instructions.InsertBefore(retIndex, new[] {
            value ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0),
            Instruction.Create(OpCodes.Stsfld, field),
        });
    }
}
