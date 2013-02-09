using System.Runtime.InteropServices;

public class ClassToTest
{
    public string Foo()
    {
        return ClassToReference.Foo();
    }
    public string Foo2()
    {
        return ClassToReference2.Foo();
    }
    public void ThrowException()
    {
        ClassToReference.ThrowException();
    }

    [DllImport("AssemblyToReferenceNative")]
    private static extern string SayHello();

    public string NativeFoo()
    {
        return SayHello();
    }
}

