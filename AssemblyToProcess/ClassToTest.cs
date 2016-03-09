﻿using System.Reflection;
using System.Runtime.InteropServices;

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
    public string Foo2()
    {
        return ClassToReference2.Foo();
    }
    public string ExeFoo()
    {
        return ExeClassToReference.Foo();
    }

    public void ThrowException()
    {
        ClassToReference.ThrowException();
    }

    [DllImport("AssemblyToReferenceNative")]
    private static extern string SayHelloFromNative();

    public string NativeFoo()
    {
        return SayHelloFromNative();
    }

    [DllImport("AssemblyToReferenceMixed")]
    private static extern string SayHelloFromMixed();

    public string MixedFooPInvoke()
    {
        return SayHelloFromMixed();
    }

    public string MixedFoo()
    {
#if MONO
        // Stub the response on Mono, we don't support building mixed assemblies
        return "Hello";
#else
        return ClassToReferenceMixed.Foo();
#endif
    }

    public Assembly GetReferencedAssembly()
    {
        return typeof(ClassToReference).Assembly;
    }
}