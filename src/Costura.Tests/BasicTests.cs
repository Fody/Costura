using System;
using System.Diagnostics;
using NUnit.Framework;

public abstract class BasicTests : BaseCosturaTest
{
    [Test, RunInApplicationDomain, Category("Code")]
    public void Simple()
    {
        var instance = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance.Simple());
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void SimplePreEmbed()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.SimplePreEmbed());
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void Exe()
    {
        var instance2 = assembly.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.Exe());
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void ThrowException()
    {
        try
        {
            var instance = assembly.GetInstance("ClassToTest");
            instance.ThrowException();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.StackTrace);
            Assert.IsTrue(exception.StackTrace.Contains("ClassToReference.cs:line"));
        }
    }

    [Test, RunInApplicationDomain, Category("Code")]
    public void TypeReferencedWithPartialAssemblyNameIsLoadedFromExistingAssemblyInstance()
    {
        var instance = assembly.GetInstance("ClassToTest");
        var assemblyLoadedByCompileTimeReference = instance.GetReferencedAssembly();
        var typeLoadedWithPartialAssemblyName = Type.GetType("ClassToReference, AssemblyToReference");
        Assume.That(typeLoadedWithPartialAssemblyName, Is.Not.Null);

        Assert.AreSame(assemblyLoadedByCompileTimeReference, typeLoadedWithPartialAssemblyName.Assembly);
    }
}