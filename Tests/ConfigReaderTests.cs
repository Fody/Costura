using System.Xml.Linq;
using Fody;
using Xunit;

public class ConfigReaderTests
{
    [Fact]
    public void CanReadFalseNode()
    {
        var xElement = XElement.Parse("<Node attr='false'/>");
        Assert.False(Configuration.ReadBool(xElement, "attr", true));
    }

    [Fact]
    public void CanReadTrueNode()
    {
        var xElement = XElement.Parse("<Node attr='true'/>");
        Assert.True(Configuration.ReadBool(xElement, "attr", false));
    }

    // These next 2 tests are because of https://github.com/Fody/Costura/issues/204

    [Fact]
    public void TrimWhitespaceFromAttributeList()
    {
        var xElement = XElement.Parse("<Node attr=' Item'/>");
        var list = Configuration.ReadList(xElement, "attr");
        Assert.Single(list);
        Assert.Equal("Item", list[0]);
    }

    [Fact]
    public void TrimWhitespaceFromElementList()
    {
        var xElement = XElement.Parse("<Node><attr>Item </attr></Node>");
        var list = Configuration.ReadList(xElement, "attr");
        Assert.Single(list);
        Assert.Equal("Item", list[0]);
    }

    [Fact]
    public void DoesNotReadInvalidBoolNode()
    {
        var xElement = XElement.Parse("<Node attr='foo'/>");
        var exception = Assert.Throws<WeavingException>(() => Configuration.ReadBool(xElement, "attr", false));
        Assert.Equal("Could not parse 'attr' from 'foo'.", exception.Message);
    }

    [Fact]
    public void FalseIncludeDebugSymbols()
    {
        var xElement = XElement.Parse("<Costura IncludeDebugSymbols='false'/>");
        var config = new Configuration(xElement);
        Assert.False(config.IncludeDebugSymbols);
    }

    [Fact]
    public void False0IncludeDebugSymbols()
    {
        var xElement = XElement.Parse("<Costura IncludeDebugSymbols='0'/>");
        var config = new Configuration(xElement);
        Assert.False(config.IncludeDebugSymbols);
    }

    [Fact]
    public void TrueDisableCompression()
    {
        var xElement = XElement.Parse("<Costura DisableCompression='true'/>");
        var config = new Configuration(xElement);
        Assert.True(config.DisableCompression);
    }

    [Fact]
    public void True1DisableCompression()
    {
        var xElement = XElement.Parse("<Costura DisableCompression='1'/>");
        var config = new Configuration(xElement);
        Assert.True(config.DisableCompression);
    }

    [Fact]
    public void TrueDisableCleanup()
    {
        var xElement = XElement.Parse("<Costura DisableCleanup='true'/>");
        var config = new Configuration(xElement);
        Assert.True(config.DisableCleanup);
    }

    [Fact]
    public void True1DisableCleanup()
    {
        var xElement = XElement.Parse("<Costura DisableCleanup='1'/>");
        var config = new Configuration(xElement);
        Assert.True(config.DisableCleanup);
    }

    [Fact]
    public void FalseLoadAtModuleInit()
    {
        var xElement = XElement.Parse("<Costura LoadAtModuleInit='false'/>");
        var config = new Configuration(xElement);
        Assert.False(config.LoadAtModuleInit);
    }

    [Fact]
    public void TrueCreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse("<Costura CreateTemporaryAssemblies='true'/>");
        var config = new Configuration(xElement);
        Assert.True(config.CreateTemporaryAssemblies);
    }

    [Fact]
    public void True1CreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse("<Costura CreateTemporaryAssemblies='1'/>");
        var config = new Configuration(xElement);
        Assert.True(config.CreateTemporaryAssemblies);
    }

    [Fact]
    public void ExcludeAssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <ExcludeAssemblies>
Foo
Bar
    </ExcludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.ExcludeAssemblies[0]);
        Assert.Equal("Bar", config.ExcludeAssemblies[1]);
    }

    [Fact]
    public void ExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura ExcludeAssemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.ExcludeAssemblies[0]);
        Assert.Equal("Bar", config.ExcludeAssemblies[1]);
    }

    [Fact]
    public void ExcludeAssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  ExcludeAssemblies='Foo'>
    <ExcludeAssemblies>
Bar
    </ExcludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.ExcludeAssemblies[0]);
        Assert.Equal("Bar", config.ExcludeAssemblies[1]);
    }

    [Fact]
    public void IncludeAssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <IncludeAssemblies>
Foo
Bar
    </IncludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.IncludeAssemblies[0]);
        Assert.Equal("Bar", config.IncludeAssemblies[1]);
    }

    [Fact]
    public void IncludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.IncludeAssemblies[0]);
        Assert.Equal("Bar", config.IncludeAssemblies[1]);
    }

    [Fact]
    public void IncludeAndExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Bar' ExcludeAssemblies='Foo'/>");
        var exception = Assert.Throws<WeavingException>(() => new Configuration(xElement));
        Assert.Equal("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.",exception.Message);
    }

    [Fact]
    public void IncludeAssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  IncludeAssemblies='Foo'>
    <IncludeAssemblies>
Bar
    </IncludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.IncludeAssemblies[0]);
        Assert.Equal("Bar", config.IncludeAssemblies[1]);
    }

    [Fact]
    public void Unmanaged32AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <Unmanaged32Assemblies>
Foo
Bar
    </Unmanaged32Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.Unmanaged32Assemblies[0]);
        Assert.Equal("Bar", config.Unmanaged32Assemblies[1]);
    }

    [Fact]
    public void Unmanaged32AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged32Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.Unmanaged32Assemblies[0]);
        Assert.Equal("Bar", config.Unmanaged32Assemblies[1]);
    }

    [Fact]
    public void Unmanaged32AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  Unmanaged32Assemblies='Foo'>
    <Unmanaged32Assemblies>
Bar
    </Unmanaged32Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.Unmanaged32Assemblies[0]);
        Assert.Equal("Bar", config.Unmanaged32Assemblies[1]);
    }

    [Fact]
    public void Unmanaged64AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <Unmanaged64Assemblies>
Foo
Bar
    </Unmanaged64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.Unmanaged64Assemblies[0]);
        Assert.Equal("Bar", config.Unmanaged64Assemblies[1]);
    }

    [Fact]
    public void Unmanaged64AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged64Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.Unmanaged64Assemblies[0]);
        Assert.Equal("Bar", config.Unmanaged64Assemblies[1]);
    }

    [Fact]
    public void Unmanaged64AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  Unmanaged64Assemblies='Foo'>
    <Unmanaged64Assemblies>
Bar
    </Unmanaged64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.Unmanaged64Assemblies[0]);
        Assert.Equal("Bar", config.Unmanaged64Assemblies[1]);
    }

    [Fact]
    public void PreloadOrderNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <PreloadOrder>
Foo
Bar
    </PreloadOrder>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.PreloadOrder[0]);
        Assert.Equal("Bar", config.PreloadOrder[1]);
    }

    [Fact]
    public void PreloadOrderAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura PreloadOrder='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.PreloadOrder[0]);
        Assert.Equal("Bar", config.PreloadOrder[1]);
    }

    [Fact]
    public void PreloadOrderCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  PreloadOrder='Foo'>
    <PreloadOrder>
Bar
    </PreloadOrder>
</Costura>");
        var config = new Configuration(xElement);
        Assert.Equal("Foo", config.PreloadOrder[0]);
        Assert.Equal("Bar", config.PreloadOrder[1]);
    }

    [Fact]
    public void IgnoreSatelliteAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IgnoreSatelliteAssemblies='True'>
</Costura>");
        var config = new Configuration(xElement);
        Assert.True(config.IgnoreSatelliteAssemblies);
    }
}