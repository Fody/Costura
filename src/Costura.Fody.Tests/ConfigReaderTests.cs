using System;
using System.Xml.Linq;
using Fody;

public class ConfigReaderTests
{
    [Test]
    public async Task CanReadFalseNode()
    {
        var xElement = XElement.Parse("<Node attr='false'/>");
        await Assert.That(Configuration.ReadBool(xElement, "attr", true)).IsFalse();
    }

    [Test]
    public async Task CanReadTrueNode()
    {
        var xElement = XElement.Parse("<Node attr='true'/>");
        await Assert.That(Configuration.ReadBool(xElement, "attr", false)).IsTrue();
    }

    // These next 2 tests are because of https://github.com/Fody/Costura/issues/204

    [Test]
    public async Task TrimWhitespaceFromAttributeList()
    {
        var xElement = XElement.Parse("<Node attr=' Item'/>");
        var list = Configuration.ReadList(xElement, "attr");
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo("Item");
    }

    [Test]
    public async Task TrimWhitespaceFromElementList()
    {
        var xElement = XElement.Parse("<Node><attr>Item </attr></Node>");
        var list = Configuration.ReadList(xElement, "attr");
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo("Item");
    }

    [Test]
    public async Task DoesNotReadInvalidBoolNode()
    {
        var xElement = XElement.Parse("<Node attr='foo'/>");
        var exception = await Assert.That(() => Configuration.ReadBool(xElement, "attr", false)).Throws<WeavingException>();
        await Assert.That(exception!.Message).IsEqualTo("Could not parse 'attr' from 'foo'.");
    }

    [Test]
    public async Task FalseIncludeDebugSymbols()
    {
        var xElement = XElement.Parse("<Costura IncludeDebugSymbols='false'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.IncludeDebugSymbols).IsFalse();
    }

    [Test]
    public async Task False0IncludeDebugSymbols()
    {
        var xElement = XElement.Parse("<Costura IncludeDebugSymbols='0'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.IncludeDebugSymbols).IsFalse();
    }

    [Test]
    public async Task TrueDisableCompression()
    {
        var xElement = XElement.Parse("<Costura DisableCompression='true'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.DisableCompression).IsTrue();
    }

    [Test]
    public async Task True1DisableCompression()
    {
        var xElement = XElement.Parse("<Costura DisableCompression='1'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.DisableCompression).IsTrue();
    }

    [Test]
    public async Task TrueDisableCleanup()
    {
        var xElement = XElement.Parse("<Costura DisableCleanup='true'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.DisableCleanup).IsTrue();
    }

    [Test]
    public async Task True1DisableCleanup()
    {
        var xElement = XElement.Parse("<Costura DisableCleanup='1'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.DisableCleanup).IsTrue();
    }

    [Test]
    public async Task TrueDisableEventSubscription()
    {
        var xElement = XElement.Parse("<Costura DisableEventSubscription='true'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.DisableEventSubscription).IsTrue();
    }

    [Test]
    public async Task True1DisableEventSubscription()
    {
        var xElement = XElement.Parse("<Costura DisableEventSubscription='1'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.DisableEventSubscription).IsTrue();
    }

    [Test]
    public async Task FalseLoadAtModuleInit()
    {
        var xElement = XElement.Parse("<Costura LoadAtModuleInit='false'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.LoadAtModuleInit).IsFalse();
    }

    [Test]
    public async Task TrueCreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse("<Costura CreateTemporaryAssemblies='true'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.CreateTemporaryAssemblies).IsTrue();
    }

    [Test]
    public async Task True1CreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse("<Costura CreateTemporaryAssemblies='1'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.CreateTemporaryAssemblies).IsTrue();
    }

    [Test]
    public async Task ExcludeAssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <ExcludeAssemblies>
Foo
Bar
    </ExcludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.ExcludeAssemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.ExcludeAssemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task ExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura ExcludeAssemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.ExcludeAssemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.ExcludeAssemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task ExcludeAssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  ExcludeAssemblies='Foo'>
    <ExcludeAssemblies>
Bar
    </ExcludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.ExcludeAssemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.ExcludeAssemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task IncludeAssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <IncludeAssemblies>
Foo
Bar
    </IncludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.IncludeAssemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.IncludeAssemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task IncludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.IncludeAssemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.IncludeAssemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task IncludeAndExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Bar' ExcludeAssemblies='Foo'/>");
        var exception = await Assert.That(() => new Configuration(xElement)).Throws<WeavingException>();
        await Assert.That(exception!.Message).IsEqualTo("Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
    }

    [Test]
    public async Task IncludeAssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  IncludeAssemblies='Foo'>
    <IncludeAssemblies>
Bar
    </IncludeAssemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.IncludeAssemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.IncludeAssemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task Unmanaged32AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <Unmanaged32Assemblies>
Foo
Bar
    </Unmanaged32Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX86Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX86Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task Unmanaged32AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged32Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX86Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX86Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task Unmanaged32AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged32Assemblies='Foo'>
    <Unmanaged32Assemblies>
Bar
    </Unmanaged32Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX86Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX86Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedX86AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <UnmanagedWinX86Assemblies>
Foo
Bar
    </UnmanagedWinX86Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX86Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX86Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedX86AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura UnmanagedWinX86Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX86Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX86Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedX86AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura UnmanagedWinX86Assemblies='Foo'>
    <UnmanagedWinX86Assemblies>
Bar
    </UnmanagedWinX86Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX86Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX86Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task Unmanaged64AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <Unmanaged64Assemblies>
Foo
Bar
    </Unmanaged64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task Unmanaged64AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged64Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task Unmanaged64AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged64Assemblies='Foo'>
    <Unmanaged64Assemblies>
Bar
    </Unmanaged64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedX64AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <UnmanagedWinX64Assemblies>
Foo
Bar
    </UnmanagedWinX64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedX64AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura UnmanagedWinX64Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedX64AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura UnmanagedWinX64Assemblies='Foo'>
    <UnmanagedWinX64Assemblies>
Bar
    </UnmanagedWinX64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinX64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinX64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedArm64AssembliesNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <UnmanagedWinArm64Assemblies>
Foo
Bar
    </UnmanagedWinArm64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinArm64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinArm64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedArm64AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura UnmanagedWinArm64Assemblies='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinArm64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinArm64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task UnmanagedArm64AssembliesCombined()
    {
        var xElement = XElement.Parse(@"
<Costura UnmanagedWinArm64Assemblies='Foo'>
    <UnmanagedWinArm64Assemblies>
Bar
    </UnmanagedWinArm64Assemblies>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.UnmanagedWinArm64Assemblies[0]).IsEqualTo("Foo");
        await Assert.That(config.UnmanagedWinArm64Assemblies[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task PreloadOrderNode()
    {
        var xElement = XElement.Parse(@"
<Costura>
    <PreloadOrder>
Foo
Bar
    </PreloadOrder>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.PreloadOrder[0]).IsEqualTo("Foo");
        await Assert.That(config.PreloadOrder[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task PreloadOrderAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura PreloadOrder='Foo|Bar'/>");
        var config = new Configuration(xElement);
        await Assert.That(config.PreloadOrder[0]).IsEqualTo("Foo");
        await Assert.That(config.PreloadOrder[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task PreloadOrderCombined()
    {
        var xElement = XElement.Parse(@"
<Costura  PreloadOrder='Foo'>
    <PreloadOrder>
Bar
    </PreloadOrder>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.PreloadOrder[0]).IsEqualTo("Foo");
        await Assert.That(config.PreloadOrder[1]).IsEqualTo("Bar");
    }

    [Test]
    public async Task IgnoreSatelliteAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IgnoreSatelliteAssemblies='True'>
</Costura>");
        var config = new Configuration(xElement);
        await Assert.That(config.IgnoreSatelliteAssemblies).IsTrue();
    }
}
