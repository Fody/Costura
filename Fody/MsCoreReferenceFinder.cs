using System.Linq;
using Mono.Cecil;

public partial class ModuleWeaver
{
    public TypeReference VoidTypeReference;
    public MethodReference CompilerGeneratedAttributeCtor;

    public void FindMsCoreReferences()
    {
        var msCoreLibDefinition = AssemblyResolver.Resolve("mscorlib");
        var msCoreTypes = msCoreLibDefinition.MainModule.Types;

        var objectDefinition = msCoreTypes.FirstOrDefault(x => x.Name == "Object");
        if (objectDefinition == null)
        {
            throw new WeavingException("Only compat with desktop .net");
        }

        var voidDefinition = msCoreTypes.First(x => x.Name == "Void");
        VoidTypeReference = ModuleDefinition.Import(voidDefinition);

        var compilerGeneratedAttribute = msCoreTypes.First(x => x.Name == "CompilerGeneratedAttribute");
        CompilerGeneratedAttributeCtor = ModuleDefinition.Import(compilerGeneratedAttribute.Methods.First(x => x.IsConstructor));
    }
}