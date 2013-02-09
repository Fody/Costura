using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public partial class ModuleWeaver
{
    public XElement Config { get; set; }
    public bool IncludeDebugSymbols = true;
    public List<string> IncludeAssemblies = new List<string>();
    public List<string> ExcludeAssemblies = new List<string>();
    public List<string> Unmanaged32Assemblies = new List<string>();
    public List<string> Unmanaged64Assemblies = new List<string>();
    public bool CreateTemporaryAssemblies;

    public void ReadConfig()
    {
        if (Config == null)
        {
            return;
        }

        ReadDebugSymbols();
        ReadCreateTemporaryAssemblies();
        ReadExcludes();
        ReadIncludes();
        ReadUnmanaged32();
        ReadUnmanaged64();

        if (IncludeAssemblies.Any() && ExcludeAssemblies.Any())
        {
            throw new WeavingException("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
        }
    }

    void ReadCreateTemporaryAssemblies()
    {
        var createTemporaryAssembliesAttribute = Config.Attribute("CreateTemporaryAssemblies");
        if (createTemporaryAssembliesAttribute != null)
        {
            if (!bool.TryParse(createTemporaryAssembliesAttribute.Value, out CreateTemporaryAssemblies))
            {
                throw new Exception(string.Format("Could not parse 'CreateTemporaryAssemblies' from '{0}'.", createTemporaryAssembliesAttribute.Value));
            }
        }
    }

    void ReadDebugSymbols()
    {
        var includeDebugAttribute = Config.Attribute("IncludeDebugSymbols");
        if (includeDebugAttribute != null)
        {
            if (!bool.TryParse(includeDebugAttribute.Value, out IncludeDebugSymbols))
            {
                throw new Exception(string.Format("Could not parse 'IncludeDebugSymbols' from '{0}'.", includeDebugAttribute.Value));
            }
        }
    }

    void ReadExcludes()
    {
        var excludeAssembliesAttribute = Config.Attribute("ExcludeAssemblies");
        if (excludeAssembliesAttribute != null)
        {
            foreach (var item in excludeAssembliesAttribute.Value.Split('|').NonEmpty())
            {
                ExcludeAssemblies.Add(item);
            }
        }

        var excludeAssembliesElement = Config.Element("ExcludeAssemblies");
        if (excludeAssembliesElement != null)
        {
            foreach (var item in excludeAssembliesElement.Value
                                                         .Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries)
                                                         .NonEmpty())
            {
                ExcludeAssemblies.Add(item);
            }
        }
    }

    void ReadIncludes()
    {
        var includeAssembliesAttribute = Config.Attribute("IncludeAssemblies");
        if (includeAssembliesAttribute != null)
        {
            foreach (var item in includeAssembliesAttribute.Value.Split('|').NonEmpty())
            {
                IncludeAssemblies.Add(item);
            }
        }

        var includeAssembliesElement = Config.Element("IncludeAssemblies");
        if (includeAssembliesElement != null)
        {
            foreach (var item in includeAssembliesElement.Value
                                                         .Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries)
                                                         .NonEmpty())
            {
                IncludeAssemblies.Add(item);
            }
        }
    }

    void ReadUnmanaged32()
    {
        var unmanagedAssembliesAttribute = Config.Attribute("Unmanaged32Assemblies");
        if (unmanagedAssembliesAttribute != null)
        {
            foreach (var item in unmanagedAssembliesAttribute.Value.Split('|').NonEmpty())
            {
                Unmanaged32Assemblies.Add(item);
            }
        }

        var unmanagedAssembliesElement = Config.Element("Unmanaged32Assemblies");
        if (unmanagedAssembliesElement != null)
        {
            foreach (var item in unmanagedAssembliesElement.Value
                                                           .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                                           .NonEmpty())
            {
                Unmanaged32Assemblies.Add(item);
            }
        }
    }

    void ReadUnmanaged64()
    {
        var unmanagedAssembliesAttribute = Config.Attribute("Unmanaged64Assemblies");
        if (unmanagedAssembliesAttribute != null)
        {
            foreach (var item in unmanagedAssembliesAttribute.Value.Split('|').NonEmpty())
            {
                Unmanaged64Assemblies.Add(item);
            }
        }

        var unmanagedAssembliesElement = Config.Element("Unmanaged64Assemblies");
        if (unmanagedAssembliesElement != null)
        {
            foreach (var item in unmanagedAssembliesElement.Value
                                                           .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                                           .NonEmpty())
            {
                Unmanaged64Assemblies.Add(item);
            }
        }
    }
}