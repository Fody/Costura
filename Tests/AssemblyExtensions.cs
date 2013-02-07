using System;
using System.Reflection;

public static class AssemblyExtensions
{
    public static dynamic GetInstance(this Assembly assembly, string className)
    {
        var type = assembly.GetType(className, true);
        //dynamic instance = FormatterServices.GetUninitializedObject(type);
        return Activator.CreateInstance(type);
    }
}