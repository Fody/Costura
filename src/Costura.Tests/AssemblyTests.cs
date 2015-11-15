using NUnit.Framework;

[TestFixture]
public class AssemblyTests : BaseCostura
{
    protected override string Suffix => "Assembly";

    [Test]
    [ExpectedException("WeavingException")]
    public void DoNotWeaveAssemblies()
    {
        CreateIsolatedAssemblyCopy("AssemblyToProcess",
                "<Costura />",
                new string[] { "AssemblyToReference.dll" },
                ".dll");
    }
}