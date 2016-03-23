#!/bin/bash
nuget restore Costura.Mono.sln

# These NuGet packages required patching for Mono compatibility; unfortunately,
# the patched versions are not yet available as a NuGet package
cp -r GitVersion/build/NuGetTaskBuild/* packages/GitVersionTask.3.3.0/
cp nunit.applicationdomain-5.0.2/bin/Debug/NUnit.ApplicationDomain/NUnit.ApplicationDomain.dll packages/NUnit.ApplicationDomain.5.0.1/lib/net40/
xbuild Costura.Mono.sln
mono NUnit-2.6.4/bin/nunit-console.exe Costura.Tests/bin/Debug/Costura.Tests.dll


