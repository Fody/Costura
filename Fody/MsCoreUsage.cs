using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver
{
    void AssertMsCoreUsages()
    {
        var assemblyLoader = ModuleDefinition.Types.First(x => x.FullName == "Costura.AssemblyLoader");
        AssertMsCoreUsages(assemblyLoader);
    }

    void AssertMsCoreUsages(TypeDefinition type)
    {
        if (IsWrongMsCoreScope(type.BaseType))
        {
            LogError(string.Format("Failed to redirect base type of '{0}' to right version of mscorlib, please contact Costura team", type.FullName));
        }

        foreach (var field in type.Fields)
        {
            if (IsWrongMsCoreScope(field.DeclaringType))
            {
                LogError(string.Format("Failed to redirect field '{0}.{1}' to right version of mscorlib, please contact Costura team", type.FullName, field.Name));
            }
        }

        foreach (var property in type.Properties)
        {
            if (IsWrongMsCoreScope(property.DeclaringType))
            {
                LogError(string.Format("Failed to redirect property '{0}.{1}' to right version of mscorlib, please contact Costura team", type.FullName, property.Name));
            }
        }

        foreach (var method in type.Methods)
        {
            if (IsWrongMsCoreScope(method.DeclaringType))
            {
                LogError(string.Format("Failed to redirect method '{0}.{1}' to right version of mscorlib, please contact Costura team", type.FullName, method.Name));
            }

            if (IsWrongMsCoreScope(method.ReturnType))
            {
                LogError(string.Format("Failed to redirect return value of method '{0}.{1}' to right version of mscorlib, please contact Costura team", type.FullName, method.Name));
            }

            foreach (var parameter in method.Parameters)
            {
                if (IsWrongMsCoreScope(parameter.ParameterType))
                {
                    LogError(string.Format("Failed to redirect return parameter '{0}' of method '{1}.{2}' to right version of mscorlib, please contact Costura team", parameter.Name, type.FullName, method.Name));
                }
            }
        }

        foreach (var nestedType in type.NestedTypes)
        {
            AssertMsCoreUsages(nestedType);
        }
    }
}
