#!/bin/bash
nuget restore
cp -r GitVersion/build/NuGetTaskBuild/* packages/GitVersionTask.3.4.1/
cp -r Stamp/NuGetBuild/* packages/Stamp.Fody.1.2.5/
xbuild
mono nunit/bin/nunit3-console.exe Tests/bin/Debug\ \(Mono\)/Tests.dll Tests2/bin/Debug\ \(Mono\)/Tests.dll Tests3/bin/Debug\ \(Mono\)/Tests.dll


