using ApprovalTests.Reporters;

[assembly: UseReporter(typeof(DiffReporter),typeof(AllFailingTestsClipboardReporter))]
//[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]
