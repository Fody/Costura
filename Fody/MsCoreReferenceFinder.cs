using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver
{
    TypeReference voidTypeReference;
    MethodReference compilerGeneratedAttributeCtor;
    MethodReference dictionaryOfStringOfStringAdd;
    MethodReference listOfStringAdd;

    void FindMsCoreReferences()
    {
        var msCoreLibDefinition = AssemblyResolver.Resolve("mscorlib");
        var msCoreTypes = msCoreLibDefinition.MainModule.Types;

        var objectDefinition = msCoreTypes.FirstOrDefault(x => x.Name == "Object");
        if (objectDefinition == null)
        {
            throw new WeavingException("Only compat with desktop .net");
        }

        var voidDefinition = msCoreTypes.First(x => x.Name == "Void");
        voidTypeReference = ModuleDefinition.Import(voidDefinition);

        var dictionary = msCoreTypes.First(x => x.Name == "Dictionary`2");
        var dictionaryOfStringOfString = ModuleDefinition.Import(dictionary);
        dictionaryOfStringOfStringAdd = ModuleDefinition.Import(dictionaryOfStringOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String, ModuleDefinition.TypeSystem.String);

        var list = msCoreTypes.First(x => x.Name == "List`1");
        var listOfString = ModuleDefinition.Import(list);
        listOfStringAdd = ModuleDefinition.Import(listOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String);

        var compilerGeneratedAttribute = msCoreTypes.First(x => x.Name == "CompilerGeneratedAttribute");
        compilerGeneratedAttributeCtor = ModuleDefinition.Import(compilerGeneratedAttribute.Methods.First(x => x.IsConstructor));
    }
}