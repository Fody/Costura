using ApprovalTests;
using ApprovalTests.Namers;
using NUnit.Framework;

public abstract class BaseCosturaTest : BaseCostura
{
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

    [Test, Category("IL")]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }
}