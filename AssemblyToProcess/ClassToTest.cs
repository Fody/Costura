using System.Runtime.InteropServices;
using FluentValidation;

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
        return ClassToReferenceMixed.Foo();
    }

    public bool Validate()
    {
        var validator = new Validator();
        return validator.Validate(this).IsValid;
    }

    private class Validator : AbstractValidator<ClassToTest>
    {
    }
}