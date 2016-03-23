using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

public class MockAssemblyResolver : IAssemblyResolver
{
    public AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        return Resolve(name.Name);
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        throw new NotImplementedException();
    }

    public AssemblyDefinition Resolve(string fullName)
    {
        var firstOrDefault = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == fullName);
        if (firstOrDefault != null)
        {
            return AssemblyDefinition.ReadAssembly(firstOrDefault.Location);
        }
        var location = Assembly.Load(fullName).Location;

        return AssemblyDefinition.ReadAssembly(location);
    }

    public AssemblyDefinition Resolve(string fullName, ReaderParameters parameters)
    {
        throw new NotImplementedException();
    }
}