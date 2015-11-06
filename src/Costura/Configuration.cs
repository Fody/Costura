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

        ReadBool(config, "IncludeDebugSymbols", b => IncludeDebugSymbols = b);
        ReadBool(config, "DisableCompression", b => DisableCompression = b);
        ReadBool(config, "CreateTemporaryAssemblies", b => CreateTemporaryAssemblies = b);

        ReadList(config, "ExcludeAssemblies", ExcludeAssemblies);
        ReadList(config, "IncludeAssemblies", IncludeAssemblies);
        ReadList(config, "Unmanaged32Assemblies", Unmanaged32Assemblies);
        ReadList(config, "Unmanaged64Assemblies", Unmanaged64Assemblies);
        ReadList(config, "PreloadOrder", PreloadOrder);

        if (IncludeAssemblies.Any() && ExcludeAssemblies.Any())
        {
            throw new WeavingException("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
        }
    }

    public bool OptOut { get; private set; }
    public bool IncludeDebugSymbols { get; private set; }
    public bool DisableCompression { get; private set; }
    public bool CreateTemporaryAssemblies { get; private set; }
    public List<string> IncludeAssemblies { get; private set; }
    public List<string> ExcludeAssemblies { get; private set; }
    public List<string> Unmanaged32Assemblies { get; private set; }
    public List<string> Unmanaged64Assemblies { get; private set; }
    public List<string> PreloadOrder { get; private set; }

    public static void ReadBool(XElement config, string nodeName, Action<bool> setter)
    {
        var attribute = config.Attribute(nodeName);
        if (attribute != null)
        {
            bool value;
            if (bool.TryParse(attribute.Value, out value))
            {
                setter(value);
            }
            else
            {
                throw new WeavingException(string.Format("Could not parse '{0}' from '{1}'.", nodeName, attribute.Value));
            }
        }
    }

    public static void ReadList(XElement config, string nodeName, List<string> list)
    {
        var attribute = config.Attribute(nodeName);
        if (attribute != null)
        {
            foreach (var item in attribute.Value.Split('|').NonEmpty())
            {
                list.Add(item);
            }
        }

        var element = config.Element(nodeName);
        if (element != null)
        {
            foreach (var item in element.Value
                                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                        .NonEmpty())
            {
                list.Add(item);
            }
        }
    }
}