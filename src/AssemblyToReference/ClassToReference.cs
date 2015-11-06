using System;
using AssemblyToReference;

public static class ClassToReference
{
    public static string Foo() => "Hello";

    public static void ThrowException()
    {
        throw new Exception("Hello");
    }

    public static string InternationalFoo() => strings.Hello;
}