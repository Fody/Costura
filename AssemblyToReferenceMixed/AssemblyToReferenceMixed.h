// AssemblyToReferenceMixed.h

#pragma once

using namespace System;

public ref class ClassToReferenceMixed
{
public:
	static String^ Foo();
	// TODO: Add your methods for this class here.
};

String^ ClassToReferenceMixed::Foo() {
	return "Hello";
};

extern "C" __declspec(dllexport) char* SayHelloFromMixed();
