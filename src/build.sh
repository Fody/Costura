#!/bin/bash
nuget restore Costura.Mono.sln
cp -r GitVersion/build/NuGetTaskBuild/* packages/GitVersionTask.3.3.0/
xbuild Costura.Mono.sln
nunit-console Costura.Tests/bin/Debug/Costura.Tests.dll


