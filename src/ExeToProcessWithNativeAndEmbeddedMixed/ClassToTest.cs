using System.Runtime.InteropServices;

public class ClassToTest
{
    [DllImport("AssemblyToReferenceNative.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.BStr)]
    private static extern string SayHelloFromNative();

    public string NativeFoo() => SayHelloFromNative();

    [DllImport("AssemblyToReferenceMixed.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.BStr)]
    private static extern string SayHelloFromMixed();

    public string MixedFooPInvoke() => SayHelloFromMixed();

    public string MixedFoo() => ClassToReferenceMixed.Foo();
}