using ApprovalTests;
using ApprovalTests.Namers;
using ApprovalTests.Writers;
using NUnit.Framework;

public abstract class BaseCosturaTest : BaseCostura
{
#if DEBUG

    private class CustomNamer : UnitTestFrameworkNamer
    {
        public string AdditionalInfo
        {
            get
            {
                var additionalInformation = NamerFactory.AdditionalInformation;
                if (additionalInformation != null)
                {
                    NamerFactory.AdditionalInformation = null;
                    additionalInformation = "." + additionalInformation;
                }
                return additionalInformation;
            }
        }

        public override string Name
        {
            get
            {
                return nameof(BaseCosturaTest.TemplateHasCorrectSymbols) + AdditionalInfo;
            }
        }
    }

    [Test, Category("IL")]
    public void TemplateHasCorrectSymbols()
    {
        using (ApprovalResults.ForScenario(Suffix))
        {
            var text = Decompiler.Decompile(afterAssemblyPath, "Costura.AssemblyLoader");
            Approvals.Verify(WriterFactory.CreateTextWriter(text), new CustomNamer(), Approvals.GetReporter());
        }
    }

#endif

    [Test, Category("IL")]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }
}