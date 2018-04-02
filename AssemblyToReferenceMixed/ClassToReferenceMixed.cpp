using namespace System;

public ref class ClassToReferenceMixed
{
public:
    static String^ Foo();
};

String^ ClassToReferenceMixed::Foo() {
    return "Hello";
};