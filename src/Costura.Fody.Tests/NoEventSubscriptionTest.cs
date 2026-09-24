using Fody;
using System;
using System.Reflection;
using System.Threading.Tasks;

#if NETCORE
using System.Runtime.Loader;
#else

#endif
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



    // Consider finalizing this test
    [Test, Explicit]
    public async Task Does_Not_Subscribe_To_Events()
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

        //await Assert.That(invocationList.Count).IsEqualTo(0);

        //await Assert.That((string)instance2.SimplePreEmbed()).IsEqualTo("Hello");
    }

    [Test]
    public async Task TemplateHasCorrectSymbols()
    {
        await VerifyHelper.AssertIlCodeAsync(TestResult.AssemblyPath);
    }
}
