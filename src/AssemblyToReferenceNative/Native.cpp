/*

C# PInvoke Code

[DllImport("AssemblyToReferenceNative.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.BStr)]
private static extern string SayHelloFromNative();

*/

#include <comdef.h>

extern "C" __declspec(dllexport) BSTR SayHelloFromNative();

BSTR SayHelloFromNative()
{
    return ::SysAllocString(L"Hello");
}