// This is the main DLL file.

#include "stdafx.h"

#include "AssemblyToReferenceMixed.h"

__declspec(dllexport) char* SayHelloFromMixed()
{
	return "Hello";
}