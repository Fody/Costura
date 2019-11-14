/*

C# PInvoke Code

[DllImport("AssemblyToReferenceMixed.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.BStr)]
private static extern string SayHelloFromMixed();

*/

#include <comdef.h>

extern "C" __declspec(dllexport) BSTR SayHelloFromMixed();

BSTR SayHelloFromMixed()
{
    return ::SysAllocString(L"Hello");
}