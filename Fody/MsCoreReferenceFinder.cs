using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

partial class ModuleWeaver
{
    List<TypeDefinition> msCoreTypes;
    TypeReference voidTypeReference;
    MethodReference compilerGeneratedAttributeCtor;
    MethodReference dictionaryOfStringOfStringAdd;
    MethodReference listOfStringAdd;

    void FindMsCoreReferences()
    {
        var msCoreLibDefinition = AssemblyResolver.Resolve("mscorlib");

        msCoreTypes = msCoreLibDefinition.MainModule.Types.ToList();

        var objectDefinition = msCoreTypes.FirstOrDefault(x => x.Name == "Object");
        if (objectDefinition == null)
        {
            throw new WeavingException("Only compat with desktop .net");
        }

        var voidDefinition = msCoreTypes.First(x => x.Name == "Void");
        voidTypeReference = ModuleDefinition.ImportReference(voidDefinition);

        var dictionary = msCoreTypes.First(x => x.Name == "Dictionary`2");
        var dictionaryOfStringOfString = ModuleDefinition.ImportReference(dictionary);
        dictionaryOfStringOfStringAdd = ModuleDefinition.ImportReference(dictionaryOfStringOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String, ModuleDefinition.TypeSystem.String);

        var list = msCoreTypes.First(x => x.Name == "List`1");
        var listOfString = ModuleDefinition.ImportReference(list);
        listOfStringAdd = ModuleDefinition.ImportReference(listOfString.Resolve().Methods.First(m => m.Name == "Add"))
            .MakeHostInstanceGeneric(ModuleDefinition.TypeSystem.String);

        var compilerGeneratedAttribute = msCoreTypes.First(x => x.Name == "CompilerGeneratedAttribute");
        compilerGeneratedAttributeCtor = ModuleDefinition.ImportReference(compilerGeneratedAttribute.Methods.First(x => x.IsConstructor));
    }
}