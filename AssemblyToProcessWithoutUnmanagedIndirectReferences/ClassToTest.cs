using System.Reflection;

public class ClassToTest
{
    public string Foo()
    {
        return ClassToIndirectReference.Foo();
    }
    public string InternationalFoo()
    {
        return ClassToIndirectReference.InternationalFoo();
    }

    public void ThrowException()
    {
        ClassToIndirectReference.ThrowException();
    }

    public Assembly GetReferencedAssembly()
    {
        return typeof(ClassToIndirectReference).Assembly;
    }
}