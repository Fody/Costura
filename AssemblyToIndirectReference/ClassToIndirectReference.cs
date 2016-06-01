public static class ClassToIndirectReference
{
    public static string Foo()
    {
        return ClassToReference.Foo();
    }
    public static void ThrowException()
    {
        ClassToReference.ThrowException();
    }
    public static string InternationalFoo()
    {
        return ClassToReference.InternationalFoo();
    }
}