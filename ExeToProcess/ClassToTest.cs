using System.Reflection;

public class ClassToTest
{
    public string Simple() => ClassToReference.Simple();

    public string InternationalFoo() => ClassToReference.InternationalFoo();

    public string SimplePreEmbed() => ClassToReferencePreEmbedded.SimplePreEmbed();

    public string Exe() => ExeClassToReference.Exe();

    public void ThrowException()
    {
        ClassToReference.ThrowException();
    }

    public Assembly GetReferencedAssembly() => typeof(ClassToReference).Assembly;
}