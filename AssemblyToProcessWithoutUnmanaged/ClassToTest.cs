using System.Reflection;

public class ClassToTest
{
    public string Foo()
    {
        return ClassToReference.Foo();
    }
    public string InternationalFoo()
    {
        return ClassToReference.InternationalFoo();
    }

    public void ThrowException()
    {
        ClassToReference.ThrowException();
    }

    public Assembly GetReferencedAssembly()
    {
        return typeof(ClassToReference).Assembly;
    }
}