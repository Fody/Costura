using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

public partial class ModuleWeaver
{
    public TypeReference VoidTypeReference;
    public MethodReference CompilerGeneratedAttributeCtor;
    public MethodReference DictionaryOfStringOfStringAdd;
    public MethodReference ListOfStringAdd;

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

        var dictionary = msCoreTypes.First(x => x.Name == "Dictionary`2");
        var dictionaryOfStringOfString = ModuleDefinition.Import(dictionary)
            .MakeGenericInstanceType(ModuleDefinition.TypeSystem.String, ModuleDefinition.TypeSystem.String);
        DictionaryOfStringOfStringAdd = ModuleDefinition.Import(dictionaryOfStringOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String, ModuleDefinition.TypeSystem.String);

        var list = msCoreTypes.First(x => x.Name == "List`1");
        var listOfString = ModuleDefinition.Import(list)
            .MakeGenericInstanceType(ModuleDefinition.TypeSystem.String);
        ListOfStringAdd = ModuleDefinition.Import(listOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String);

        var compilerGeneratedAttribute = msCoreTypes.First(x => x.Name == "CompilerGeneratedAttribute");
        CompilerGeneratedAttributeCtor = ModuleDefinition.Import(compilerGeneratedAttribute.Methods.First(x => x.IsConstructor));
    }
}