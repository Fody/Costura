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

        ReadDebugSymbols();
        ReadCompression();
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
            bool createTemporaryAssemblies;
            if (bool.TryParse(createTemporaryAssembliesAttribute.Value, out createTemporaryAssemblies))
            {
                CreateTemporaryAssemblies = createTemporaryAssemblies;
            }
            else
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
            bool includeDebugSymbols;
            if (bool.TryParse(includeDebugAttribute.Value, out includeDebugSymbols))
            {
                IncludeDebugSymbols = includeDebugSymbols;
            }
            else
            {
                throw new Exception(string.Format("Could not parse 'IncludeDebugSymbols' from '{0}'.", includeDebugAttribute.Value));
            }
        }
    }

    void ReadCompression()
    {
        var disableCompressionAttribute = Config.Attribute("DisableCompression");
        if (disableCompressionAttribute != null)
        {
            bool disableCompression;
            if (bool.TryParse(disableCompressionAttribute.Value, out disableCompression))
            {
                DisableCompression = disableCompression;
            }
            else
            {
                throw new Exception(string.Format("Could not parse 'DisableCompression' from '{0}'.", disableCompressionAttribute.Value));
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
                                                         .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
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
                                                         .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
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