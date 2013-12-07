using System;
using System.Xml.Linq;
using NUnit.Framework;

[TestFixture]
public class ConfigReaderTests
{
    [Test]
    public void CanReadFalseNode()
    {
        var xElement = XElement.Parse(@"<Node attr='false'/>");
        ModuleWeaver.ReadBool(xElement, "attr", b => Assert.IsFalse(b));
    }

    [Test]
    public void CanReadTrueNode()
    {
        var xElement = XElement.Parse(@"<Node attr='true'/>");
        ModuleWeaver.ReadBool(xElement, "attr", b => Assert.IsTrue(b));
    }

    [Test]
    public void DoesNotReadInvalidBoolNode()
    {
        var xElement = XElement.Parse(@"<Node attr='foo'/>");
        Assert.Throws<WeavingException>(() => ModuleWeaver.ReadBool(xElement, "attr", b => Assert.Fail()), "Could not parse 'attr' from 'foo'.");
    }

    [Test]
    public void DoesNotSetBoolWhenNodeMissing()
    {
        var xElement = XElement.Parse(@"<Node attr='false'/>");
        ModuleWeaver.ReadBool(xElement, "missing", b => Assert.Fail());
    }

    [Test]
    public void FalseIncludeDebugSymbols()
    {
        var xElement = XElement.Parse(@"<Costura IncludeDebugSymbols='false'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.IsFalse(moduleWeaver.IncludeDebugSymbols);
    }

    [Test]
    public void TrueDisableCompression()
    {
        var xElement = XElement.Parse(@"<Costura DisableCompression='true'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.IsTrue(moduleWeaver.DisableCompression);
    }

    [Test]
    public void TrueCreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse(@"<Costura CreateTemporaryAssemblies='true'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.IsTrue(moduleWeaver.CreateTemporaryAssemblies);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.ExcludeAssemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.ExcludeAssemblies[1]);
    }

    [Test]
    public void ExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura ExcludeAssemblies='Foo|Bar'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.ExcludeAssemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.ExcludeAssemblies[1]);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.ExcludeAssemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.ExcludeAssemblies[1]);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.IncludeAssemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.IncludeAssemblies[1]);
    }

    [Test]
    public void IncludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Foo|Bar'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.IncludeAssemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.IncludeAssemblies[1]);
    }

    [Test]
    public void IncludeAndExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Bar' ExcludeAssemblies='Foo'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        Assert.Throws<WeavingException>(() => moduleWeaver.ReadConfig(), "Either configure IncludeAssemblies OR ExcludeAssemblies, not both.");
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.IncludeAssemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.IncludeAssemblies[1]);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.Unmanaged32Assemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.Unmanaged32Assemblies[1]);
    }

    [Test]
    public void Unmanaged32AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged32Assemblies='Foo|Bar'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.Unmanaged32Assemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.Unmanaged32Assemblies[1]);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.Unmanaged32Assemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.Unmanaged32Assemblies[1]);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.Unmanaged64Assemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.Unmanaged64Assemblies[1]);
    }

    [Test]
    public void Unmanaged64AssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura Unmanaged64Assemblies='Foo|Bar'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.Unmanaged64Assemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.Unmanaged64Assemblies[1]);
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
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
        Assert.AreEqual("Foo", moduleWeaver.Unmanaged64Assemblies[0]);
        Assert.AreEqual("Bar", moduleWeaver.Unmanaged64Assemblies[1]);
    }
}