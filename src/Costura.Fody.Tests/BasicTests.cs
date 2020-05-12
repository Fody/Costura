using System;
using System.Diagnostics;
using ApprovalTests;
using ApprovalTests.Namers;
using NUnit.Framework;

public abstract class BasicTests : BaseCosturaTest
{
    [Test]
    public void Simple()
    {
        var instance = TestResult.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance.Simple());
    }

    [Test]
    public void SimplePreEmbed()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.SimplePreEmbed());
    }

    [Test]
    public void Exe()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");
        Assert.AreEqual("Hello", instance2.Exe());
    }

    [Test]
    public void ThrowException()
    {
        try
        {
            var instance = TestResult.GetInstance("ClassToTest");
            instance.ThrowException();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.StackTrace);
            Assert.IsTrue(exception.StackTrace.Contains("ClassToReference.cs:line"));
        }
    }

    [Test]
    public void TypeReferencedWithPartialAssemblyNameIsLoadedFromExistingAssemblyInstance()
    {
        var instance = TestResult.GetInstance("ClassToTest");
        var assemblyLoadedByCompileTimeReference = instance.GetReferencedAssembly();
        var typeName = "ClassToReference, AssemblyToReference";
        if (TestResult.Assembly.GetName().Name.EndsWith("35"))
        {
            typeName = typeName + "35";
        }
        var typeLoadedWithPartialAssemblyName = Type.GetType(typeName);
        Assert.NotNull(typeLoadedWithPartialAssemblyName);

        Assert.AreEqual(assemblyLoadedByCompileTimeReference, typeLoadedWithPartialAssemblyName.Assembly);
    }

    [Test]
    public void TemplateHasCorrectSymbols()
    {
        var dataPoints = GetScenarioName();

        using (ApprovalResults.ForScenario(dataPoints))
        {
            var text = Ildasm.Decompile(TestResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}
