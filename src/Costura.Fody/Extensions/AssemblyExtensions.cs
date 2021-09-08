using System;
using System.Linq;
using System.Reflection;

internal static class AssemblyExtensions
{
    public static string GetVersion(this Assembly assembly)
    {
        var version = GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(assembly);
        return version is null ? null : version.InformationalVersion;
    }

    private static TAttibute GetAssemblyAttribute<TAttibute>(Assembly assembly)
        where TAttibute : Attribute
    {
        var attibutes = assembly.GetCustomAttributes(typeof(TAttibute))?.ToArray() ?? Array.Empty<Attribute>();
        return attibutes.Length > 0 ? attibutes[0] as TAttibute : null;
    }
}
