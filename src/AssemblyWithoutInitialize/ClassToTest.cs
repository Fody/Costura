﻿using System;
using System.Reflection;
// ReSharper disable UnusedVariable

public class ClassToTest
{
    static ClassToTest()
    {
        // Produces an instruction with the method as an operand.
        // ldftn System.Void CosturaUtility::Initialize()
        var initialize = CosturaUtility.Initialize;
    }

    public string Simple() => ClassToReference.Simple();

    public string InternationalFoo() => ClassToReference.InternationalFoo();

    public string SimplePreEmbed() => ClassToReferencePreEmbedded.SimplePreEmbed();

    public string Exe() => ExeClassToReference.Exe();

    public void ThrowException()
    {
        ClassToReference.ThrowException();
    }

    public Assembly GetReferencedAssembly() => typeof(ClassToReference).Assembly;
}
