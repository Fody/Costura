#!/bin/bash
nuget restore Costura.Mono.sln
cp -r GitVersion/build/NuGetTaskBuild/* packages/GitVersionTask.3.3.0/
xbuild Costura.Mono.sln /p:DefineConstants=DEBUG;MONO
mono NUnit-2.6.4/bin/nunit-console.exe Costura.Tests/bin/Debug/Costura.Tests.dll


