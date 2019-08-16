using ApprovalTests.Reporters;
using Xunit;

[assembly: UseReporter(typeof(DiffReporter),typeof(AllFailingTestsClipboardReporter))]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]