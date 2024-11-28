using System.Linq;
using Mono.Cecil;

public partial class ModuleWeaver
{
    private MethodReference _compilerGeneratedAttributeCtor;
    private MethodReference _dictionaryOfStringOfStringAdd;
    private MethodReference _listOfStringAdd;

    private void FindMsCoreReferences()
    {
        var dictionary = FindTypeDefinition("System.Collections.Generic.Dictionary`2");
        var dictionaryOfStringOfString = ModuleDefinition.ImportReference(dictionary);
        _dictionaryOfStringOfStringAdd = ModuleDefinition.ImportReference(dictionaryOfStringOfString.Resolve().Methods.First(_ => _.Name == "Add"))
            .MakeHostInstanceGeneric(TypeSystem.StringReference, TypeSystem.StringReference);

        var list = FindTypeDefinition("System.Collections.Generic.List`1");
        var listOfString = ModuleDefinition.ImportReference(list);
        _listOfStringAdd = ModuleDefinition.ImportReference(listOfString.Resolve().Methods.First(_ => _.Name == "Add"))
            .MakeHostInstanceGeneric(TypeSystem.StringReference);

        var compilerGeneratedAttribute = FindTypeDefinition("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        _compilerGeneratedAttributeCtor = ModuleDefinition.ImportReference(compilerGeneratedAttribute.Methods.First(_ => _.IsConstructor));
    }
}
