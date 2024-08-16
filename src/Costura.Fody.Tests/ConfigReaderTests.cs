using System.Xml.Linq;
using Fody;
using NUnit.Framework;

[TestFixture]
public class ConfigReaderTests
{
    [Test]
    public void CanReadFalseNode()
    {
        var xElement = XElement.Parse("<Node attr='false'/>");
        Assert.That(Configuration.ReadBool(xElement, "attr", true), Is.False);
    }

    [Test]
    public void CanReadTrueNode()
    {
        var xElement = XElement.Parse("<Node attr='true'/>");
        Assert.That(Configuration.ReadBool(xElement, "attr", false), Is.True);
    }

    // These next 2 tests are because of https://github.com/Fody/Costura/issues/204

    [Test]
    public void TrimWhitespaceFromAttributeList()
    {
        var xElement = XElement.Parse("<Node attr=' Item'/>");
        var list = Configuration.ReadList(xElement, "attr");
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo("Item"));
    }

    [Test]
    public void TrimWhitespaceFromElementList()
    {
        var xElement = XElement.Parse("<Node><attr>Item </attr></Node>");
        var list = Configuration.ReadList(xElement, "attr");
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo("Item"));
    }

    [Test]
    public void DoesNotReadInvalidBoolNode()
    {
        var xElement = XElement.Parse("<Node attr='foo'/>");
        var exception = Assert.Throws<WeavingException>(() => Configuration.ReadBool(xElement, "attr", false));
        Assert.That(exception.Message, Is.EqualTo("Could not parse 'attr' from 'foo'."));
    }

    [Test]
    public void FalseIncludeDebugSymbols()
    {
        var xElement = XElement.Parse("<Costura IncludeDebugSymbols='false'/>");
        var config = new Configuration(xElement);
        Assert.That(config.IncludeDebugSymbols, Is.False);
    }

    [Test]
    public void False0IncludeDebugSymbols()
    {
        var xElement = XElement.Parse("<Costura IncludeDebugSymbols='0'/>");
        var config = new Configuration(xElement);
        Assert.That(config.IncludeDebugSymbols, Is.False);
    }

    [Test]
    public void TrueDisableCompression()
    {
        var xElement = XElement.Parse("<Costura DisableCompression='true'/>");
        var config = new Configuration(xElement);
        Assert.That(config.DisableCompression, Is.True);
    }

    [Test]
    public void True1DisableCompression()
    {
        var xElement = XElement.Parse("<Costura DisableCompression='1'/>");
        var config = new Configuration(xElement);
        Assert.That(config.DisableCompression, Is.True);
    }

    [Test]
    public void TrueDisableCleanup()
    {
        var xElement = XElement.Parse("<Costura DisableCleanup='true'/>");
        var config = new Configuration(xElement);
        Assert.That(config.DisableCleanup, Is.True);
    }

    [Test]
    public void True1DisableCleanup()
    {
        var xElement = XElement.Parse("<Costura DisableCleanup='1'/>");
        var config = new Configuration(xElement);
        Assert.That(config.DisableCleanup, Is.True);
    }

    [Test]
    public void FalseLoadAtModuleInit()
    {
        var xElement = XElement.Parse("<Costura LoadAtModuleInit='false'/>");
        var config = new Configuration(xElement);
        Assert.That(config.LoadAtModuleInit, Is.False);
    }

    [Test]
    public void TrueCreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse("<Costura CreateTemporaryAssemblies='true'/>");
        var config = new Configuration(xElement);
        Assert.That(config.CreateTemporaryAssemblies, Is.True);
    }

    [Test]
    public void True1CreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse("<Costura CreateTemporaryAssemblies='1'/>");
        var config = new Configuration(xElement);
        Assert.That(config.CreateTemporaryAssemblies, Is.True);
    }

    [Test]
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
        Assert.That(config.ExcludeAssemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.ExcludeAssemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void ExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura ExcludeAssemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.That(config.ExcludeAssemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.ExcludeAssemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void ExcludeAssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  ExcludeAssemblies='Foo'>
    <ExcludeAssemblies>
Bar
    </ExcludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.That(config.ExcludeAssemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.ExcludeAssemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
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
        Assert.That(config.IncludeAssemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.IncludeAssemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void IncludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.That(config.IncludeAssemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.IncludeAssemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void IncludeAndExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Bar' ExcludeAssemblies='Foo'/>");
        var exception = Assert.Throws<WeavingException>(() => new Configuration(xElement));
        Assert.That(exception.Message, Is.EqualTo("Either configure IncludeAssemblies OR ExcludeAssemblies, not both."));
    }

    [Test]
    public void IncludeAssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  IncludeAssemblies='Foo'>
    <IncludeAssemblies>
Bar
    </IncludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.That(config.IncludeAssemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.IncludeAssemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
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
        Assert.That(config.Unmanaged32Assemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.Unmanaged32Assemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void Unmanaged32AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged32Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.That(config.Unmanaged32Assemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.Unmanaged32Assemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void Unmanaged32AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  Unmanaged32Assemblies='Foo'>
    <Unmanaged32Assemblies>
Bar
    </Unmanaged32Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.That(config.Unmanaged32Assemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.Unmanaged32Assemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
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
        Assert.That(config.Unmanaged64Assemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.Unmanaged64Assemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void Unmanaged64AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged64Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.That(config.Unmanaged64Assemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.Unmanaged64Assemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void Unmanaged64AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  Unmanaged64Assemblies='Foo'>
    <Unmanaged64Assemblies>
Bar
    </Unmanaged64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        Assert.That(config.Unmanaged64Assemblies[0], Is.EqualTo("Foo"));
        Assert.That(config.Unmanaged64Assemblies[1], Is.EqualTo("Bar"));
    }

    [Test]
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
        Assert.That(config.PreloadOrder[0], Is.EqualTo("Foo"));
        Assert.That(config.PreloadOrder[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void PreloadOrderAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura PreloadOrder='Foo|Bar'/>");
        var config = new Configuration(xElement);
        Assert.That(config.PreloadOrder[0], Is.EqualTo("Foo"));
        Assert.That(config.PreloadOrder[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void PreloadOrderCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  PreloadOrder='Foo'>
    <PreloadOrder>
Bar
    </PreloadOrder>
</Costura>");
        var config = new Configuration(xElement);
        Assert.That(config.PreloadOrder[0], Is.EqualTo("Foo"));
        Assert.That(config.PreloadOrder[1], Is.EqualTo("Bar"));
    }

    [Test]
    public void IgnoreSatelliteAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IgnoreSatelliteAssemblies='True'>
</Costura>");
        var config = new Configuration(xElement);
        Assert.That(config.IgnoreSatelliteAssemblies, Is.True);
    }
}
