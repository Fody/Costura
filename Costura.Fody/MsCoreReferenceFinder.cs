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
        var voidDefinition = FindType("System.Void");
        voidTypeReference = ModuleDefinition.ImportReference(voidDefinition);

        var dictionary = FindType("System.Collections.Generic.Dictionary`2");
        var dictionaryOfStringOfString = ModuleDefinition.ImportReference(dictionary);
        dictionaryOfStringOfStringAdd = ModuleDefinition.ImportReference(dictionaryOfStringOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String, ModuleDefinition.TypeSystem.String);

        var list = FindType("System.Collections.Generic.List`1");
        var listOfString = ModuleDefinition.ImportReference(list);
        listOfStringAdd = ModuleDefinition.ImportReference(listOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String);

        var compilerGeneratedAttribute = FindType("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        compilerGeneratedAttributeCtor = ModuleDefinition.ImportReference(compilerGeneratedAttribute.Methods.First(x => x.IsConstructor));
    }
}