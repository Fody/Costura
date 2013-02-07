using System.Xml.Linq;
using NUnit.Framework;

[TestFixture]
public class ConfigReaderTests
{
    [Test]
    public void FalseIncludeDebugSymbols()
    {
        var xElement = XElement.Parse(@"<Costura IncludeDebugSymbols='false'/>");
        var moduleWeaver = new ModuleWeaver {Config = xElement};
        moduleWeaver.ReadConfig();
        Assert.IsFalse( moduleWeaver.IncludeDebugSymbols);
    }

    [Test]
    public void TrueCreateTemporaryAssemblies()
    {
        var xElement = XElement.Parse(@"<Costura CreateTemporaryAssemblies='true'/>");
        var moduleWeaver = new ModuleWeaver {Config = xElement};
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
    public void ExcludeAssembliesConbined()
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
    [ExpectedException(ExpectedMessage = "Either configure IncludeAssemblies OR ExcludeAssemblies, not both.")]
    public void IncludeAndExcludeAssembliesAttribute()
    {
        var xElement = XElement.Parse(@"
<Costura IncludeAssemblies='Bar' ExcludeAssemblies='Foo'/>");
        var moduleWeaver = new ModuleWeaver { Config = xElement };
        moduleWeaver.ReadConfig();
    }

    [Test]
    public void IncludeAssembliesConbined()
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

}