using System;
using System.Diagnostics;
using System.Threading.Tasks;

public abstract class BasicTests : BaseCosturaTest
{
    [Test]
    public async Task Simple()
    {
        var instance = TestResult.GetInstance("ClassToTest");
        await Assert.That((string)instance.Simple()).IsEqualTo("Hello");
    }

    [Test]
    public async Task SimplePreEmbed()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");
        await Assert.That((string)instance2.SimplePreEmbed()).IsEqualTo("Hello");
    }

    [Test]
    public async Task Exe()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");
        await Assert.That((string)instance2.Exe()).IsEqualTo("Hello");
    }

    [Test]
    public async Task ThrowException()
    {
        try
        {
            var instance = TestResult.GetInstance("ClassToTest");
            instance.ThrowException();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.StackTrace);
            await Assert.That(exception.StackTrace.Contains("ClassToReference.cs:line")).IsTrue();
        }
    }

    [Test]
    public async Task TypeReferencedWithPartialAssemblyNameIsLoadedFromExistingAssemblyInstance()
    {
        var instance = TestResult.GetInstance("ClassToTest");
        var assemblyLoadedByCompileTimeReference = instance.GetReferencedAssembly();
        var typeName = "ClassToReference, AssemblyToReference";
        if (TestResult.Assembly.GetName().Name.EndsWith("35"))
        {
            typeName = typeName + "35";
        }
        var typeLoadedWithPartialAssemblyName = Type.GetType(typeName);
        await Assert.That(typeLoadedWithPartialAssemblyName).IsNotNull();

        await Assert.That((object)assemblyLoadedByCompileTimeReference).IsEqualTo(typeLoadedWithPartialAssemblyName.Assembly);
    }

    [Test]
    public async Task TemplateHasCorrectSymbols()
    {
        await VerifyHelper.AssertIlCodeAsync(TestResult.AssemblyPath);
    }
}
