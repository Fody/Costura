#if MONO
#else
using ApprovalTests;
using ApprovalTests.Namers;
#endif
using NUnit.Framework;

public abstract class BaseCosturaTest : BaseCostura
{
#if MONO
#else
#if DEBUG

    [Test, Category("IL")]
    public void TemplateHasCorrectSymbols()
    {
        using (ApprovalResults.ForScenario(Suffix))
        {
            Approvals.Verify(Decompiler.Decompile(afterAssemblyPath, "Costura.AssemblyLoader"));
        }
    }

#endif
#endif

    [Test, Category("IL")]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }
}
