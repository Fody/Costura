using ApprovalTests.Namers;
using ApprovalTests;
using Fody;
using NUnit.Framework;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

#if NETCORE
using System.Runtime.Loader;
#else

#endif

[TestFixture]
public class NoEventSubscriptionTest : BaseCosturaTest
{
    private static readonly TestResult testResult;

    static NoEventSubscriptionTest()
    {
        testResult = WeavingHelper.CreateIsolatedAssemblyCopy("AssemblyToProcess.dll",
            "<Costura DisableEventSubscription='true' />",
            new[] { "AssemblyToReference.dll", "AssemblyToReferencePreEmbedded.dll", "ExeToReference.exe" },
            "DisableEventSubscription");
    }

    public override TestResult TestResult => testResult;



    [Test, Explicit("Consider finalizing this test")]
    public void Does_Not_Subscribe_To_Events()
    {
        var instance2 = TestResult.GetInstance("ClassToTest");

        EventInfo eventInfo = null;
        object instance = null;

#if NETCORE
        instance = AssemblyLoadContext.Default;
        eventInfo = typeof(AssemblyLoadContext).GetEvent("Resolving");
#else
        instance = AppDomain.CurrentDomain;
        eventInfo = typeof(AppDomain).GetEvent("AssemblyResolve");
#endif

        //var eventField = instance.GetType().GetRuntimeFields().Single(x => x.Name == $"_{eventInfo.Name}");

        //var eventInstance = (EventHandler)eventField.GetValue(instance);

        //var invocationList = eventInstance.GetInvocationList();

        //Assert.That(invocationList.Count, Is.EqualTo(0));

        //Assert.That("Hello", Is.EqualTo(instance2.SimplePreEmbed()));
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
