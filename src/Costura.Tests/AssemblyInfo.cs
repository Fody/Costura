#if MONO
#else
﻿using ApprovalTests.Namers;
using ApprovalTests.Reporters;

[assembly: UseReporter(typeof(DiffReporter))]
[assembly: UseApprovalSubdirectory("Approvals")]
#endif
