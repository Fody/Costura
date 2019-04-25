using System;
using System.Diagnostics;
using ApprovalTests;
using ApprovalTests.Namers;
using Xunit;

public abstract class BasicTests : BaseCosturaTest
{
    [Fact]
    public void Simple()
    {
        var instance = TestResult.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance.Simple());
    }

    [Fact]
    public void SimplePreEmbed()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance2.SimplePreEmbed());
    }

    [Fact]
    public void Exe()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");
        Assert.Equal("Hello", instance2.Exe());
    }

    [Fact]
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
            Assert.Contains("ClassToReference.cs:line", exception.StackTrace);
        }
    }

    [Fact]
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

        Assert.Same(assemblyLoadedByCompileTimeReference, typeLoadedWithPartialAssemblyName.Assembly);
    }

    [Fact]
    public void TemplateHasCorrectSymbols()
    {
#if DEBUG
        var dataPoints = GetType().Name + "Debug";
#else
        var dataPoints = GetType().Name + "Release";
#endif
        using (ApprovalResults.ForScenario(dataPoints))
        {
            var text = Ildasm.Decompile(TestResult.AssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(text);
        }
    }
}