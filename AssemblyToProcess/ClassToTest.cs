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
}

