using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

partial class ModuleWeaver
{
    public XElement Config { get; set; }
    public bool IncludeDebugSymbols { get; private set; }
    public bool DisableCompression { get; private set; }
    public bool CreateTemporaryAssemblies { get; private set; }
    public List<string> IncludeAssemblies { get; private set; }
    public List<string> ExcludeAssemblies { get; private set; }
    public List<string> Unmanaged32Assemblies { get; private set; }
    public List<string> Unmanaged64Assemblies { get; private set; }

    public void ReadConfig()
    {
        // Defaults
        IncludeDebugSymbols = true;
        DisableCompression = false;
        CreateTemporaryAssemblies = false;
        IncludeAssemblies = new List<string>();
        ExcludeAssemblies = new List<string>();
        Unmanaged32Assemblies = new List<string>();
        Unmanaged64Assemblies = new List<string>();

        if (Config == null)
        {
            return;
        }

        ReadBool(Config, "IncludeDebugSymbols", b => IncludeDebugSymbols = b);
        ReadBool(Config, "DisableCompression", b => DisableCompression = b);
        ReadBool(Config, "CreateTemporaryAssemblies", b => CreateTemporaryAssemblies = b);

        ReadList(Config, "ExcludeAssemblies", ExcludeAssemblies);
        ReadList(Config, "IncludeAssemblies", IncludeAssemblies);
        ReadList(Config, "Unmanaged32Assemblies", Unmanaged32Assemblies);
        ReadList(Config, "Unmanaged64Assemblies", Unmanaged64Assemblies);

        if (IncludeAssemblies.Any() && ExcludeAssemblies.Any())
        {
            throw new WeavingException("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
        }
    }

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