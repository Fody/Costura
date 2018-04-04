using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public class Configuration
{
    public Configuration(XElement config)
    {
        // Defaults
        OptOut = true;
        IncludeDebugSymbols = true;
        DisableCompression = false;
        DisableCleanup = false;
        LoadAtModuleInit = true;
        CreateTemporaryAssemblies = false;
        IncludeAssemblies = new List<string>();
        ExcludeAssemblies = new List<string>();
        Unmanaged32Assemblies = new List<string>();
        Unmanaged64Assemblies = new List<string>();
        PreloadOrder = new List<string>();

        if (config == null)
        {
            return;
        }

        if (config.Attribute("IncludeAssemblies") != null || config.Element("IncludeAssemblies") != null)
        {
            OptOut = false;
        }

        IncludeDebugSymbols = ReadBool(config, "IncludeDebugSymbols", IncludeDebugSymbols);
        DisableCompression = ReadBool(config, "DisableCompression", DisableCompression);
        DisableCleanup = ReadBool(config, "DisableCleanup", DisableCleanup);
        LoadAtModuleInit = ReadBool(config, "LoadAtModuleInit", LoadAtModuleInit);
        CreateTemporaryAssemblies = ReadBool(config, "CreateTemporaryAssemblies", CreateTemporaryAssemblies);

        ExcludeAssemblies = ReadList(config, "ExcludeAssemblies");
        IncludeAssemblies = ReadList(config, "IncludeAssemblies");
        Unmanaged32Assemblies = ReadList(config, "Unmanaged32Assemblies");
        Unmanaged64Assemblies = ReadList(config, "Unmanaged64Assemblies");
        PreloadOrder = ReadList(config, "PreloadOrder");

        if (IncludeAssemblies.Any() && ExcludeAssemblies.Any())
        {
            throw new WeavingException("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
        }
    }

    public bool OptOut { get; }
    public bool IncludeDebugSymbols { get; }
    public bool DisableCompression { get; }
    public bool DisableCleanup { get; }
    public bool LoadAtModuleInit { get; }
    public bool CreateTemporaryAssemblies { get; }
    public List<string> IncludeAssemblies { get; }
    public List<string> ExcludeAssemblies { get; }
    public List<string> Unmanaged32Assemblies { get; }
    public List<string> Unmanaged64Assemblies { get; }
    public List<string> PreloadOrder { get; }

    public static bool ReadBool(XElement config, string nodeName, bool @default)
    {
        var attribute = config.Attribute(nodeName);
        if (attribute != null)
        {
            if (bool.TryParse(attribute.Value, out var value))
            {
                return value;
            }
            else
            {
                throw new WeavingException($"Could not parse '{nodeName}' from '{attribute.Value}'.");
            }
        }

        return @default;
    }

    public static List<string> ReadList(XElement config, string nodeName)
    {
        var list = new List<string>();

        var attribute = config.Attribute(nodeName);
        if (attribute != null)
        {
            foreach (var item in attribute.Value.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                list.Add(item.Trim());
            }
        }

        var element = config.Element(nodeName);
        if (element != null)
        {
            foreach (var item in element.Value
                                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                list.Add(item.Trim());
            }
        }

        return list;
    }
}