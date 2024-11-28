using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Fody;

public class Configuration
{
    public Configuration(XElement config)
    {
        // Defaults
        OptOutAssemblies = true;
        IncludeDebugSymbols = true;
        IncludeRuntimeReferences = true;
        UseRuntimeReferencePaths = null;
        DisableCompression = false;
        DisableCleanup = false;
        DisableEventSubscription = false;
        LoadAtModuleInit = true;
        CreateTemporaryAssemblies = false;
        IgnoreSatelliteAssemblies = false;
        IncludeAssemblies = new List<string>();
        ExcludeAssemblies = new List<string>();
        IncludeRuntimeAssemblies = new List<string>();
        ExcludeRuntimeAssemblies = new List<string>();
        UnmanagedWinX86Assemblies = new List<string>();
        UnmanagedWinX64Assemblies = new List<string>();
        UnmanagedWinArm64Assemblies = new List<string>();
        PreloadOrder = new List<string>();

        if (config is null)
        {
            return;
        }

        if (config.Attribute("IncludeAssemblies") is not null ||
            config.Element("IncludeAssemblies") is not null)
        {
            OptOutAssemblies = false;
        }

        if (config.Attribute("IncludeRuntimeAssemblies") is not null ||
            config.Element("IncludeRuntimeAssemblies") is not null)
        {
            OptOutRuntimeAssemblies = false;
        }

        IncludeDebugSymbols = ReadBool(config, "IncludeDebugSymbols", IncludeDebugSymbols);
        IncludeRuntimeReferences = ReadBool(config, "IncludeRuntimeReferences", IncludeRuntimeReferences);
        UseRuntimeReferencePaths = ReadBool(config, "UseRuntimeReferencePaths");
        DisableCompression = ReadBool(config, "DisableCompression", DisableCompression);
        DisableCleanup = ReadBool(config, "DisableCleanup", DisableCleanup);
        DisableEventSubscription = ReadBool(config, "DisableEventSubscription", DisableEventSubscription);
        LoadAtModuleInit = ReadBool(config, "LoadAtModuleInit", LoadAtModuleInit);
        CreateTemporaryAssemblies = ReadBool(config, "CreateTemporaryAssemblies", CreateTemporaryAssemblies);
        IgnoreSatelliteAssemblies = ReadBool(config, "IgnoreSatelliteAssemblies", IgnoreSatelliteAssemblies);

        ExcludeAssemblies = ReadList(config, "ExcludeAssemblies");
        IncludeAssemblies = ReadList(config, "IncludeAssemblies");
        ExcludeRuntimeAssemblies = ReadList(config, "ExcludeRuntimeAssemblies");
        IncludeRuntimeAssemblies = ReadList(config, "IncludeRuntimeAssemblies");

        UnmanagedWinX86Assemblies = ReadList(config, "UnmanagedWinX86Assemblies");
        if (!UnmanagedWinX86Assemblies.Any())
        {
            // Backwards compatibility
            UnmanagedWinX86Assemblies = ReadList(config, "Unmanaged32Assemblies");
        }

        UnmanagedWinX64Assemblies = ReadList(config, "UnmanagedWinX64Assemblies");
        if (!UnmanagedWinX64Assemblies.Any())
        {
            // Backwards compatibility
            UnmanagedWinX64Assemblies = ReadList(config, "Unmanaged64Assemblies");
        }

        UnmanagedWinArm64Assemblies = ReadList(config, "UnmanagedWinArm64Assemblies");

        PreloadOrder = ReadList(config, "PreloadOrder");

        if (IncludeAssemblies.Any() && ExcludeAssemblies.Any())
        {
            throw new WeavingException("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
        }
    }

    public bool OptOutAssemblies { get; }
    public bool OptOutRuntimeAssemblies { get; }
    public bool IncludeDebugSymbols { get; }
    public bool IncludeRuntimeReferences { get; }
    public bool? UseRuntimeReferencePaths { get; }
    public bool DisableCompression { get; }
    public bool DisableCleanup { get; }
    public bool DisableEventSubscription { get; }
    
    public bool LoadAtModuleInit { get; }
    public bool CreateTemporaryAssemblies { get; }
    public bool IgnoreSatelliteAssemblies { get; }
    public List<string> IncludeAssemblies { get; }
    public List<string> ExcludeAssemblies { get; }
    public List<string> IncludeRuntimeAssemblies { get; }
    public List<string> ExcludeRuntimeAssemblies { get; }
    public List<string> UnmanagedWinX86Assemblies { get; }
    public List<string> UnmanagedWinX64Assemblies { get; }
    public List<string> UnmanagedWinArm64Assemblies { get; }
    public List<string> PreloadOrder { get; }

    public static bool ReadBool(XElement config, string nodeName, bool @default)
    {
        return ReadBool(config, nodeName) ?? @default;
    }

    public static bool? ReadBool(XElement config, string nodeName)
    {
        var attribute = config.Attribute(nodeName);
        if (attribute is not null)
        {
            try
            {
                return XmlConvert.ToBoolean(attribute.Value.ToLowerInvariant());
            }
            catch
            {
                throw new WeavingException($"Could not parse '{nodeName}' from '{attribute.Value}'.");
            }
        }

        return null;
    }

    public static List<string> ReadList(XElement config, string nodeName)
    {
        var list = new List<string>();

        var attribute = config.Attribute(nodeName);
        if (attribute is not null)
        {
            foreach (var item in attribute.Value.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                list.Add(item.Trim());
            }
        }

        var element = config.Element(nodeName);
        if (element is not null)
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
