#!/bin/bash
wget https://github.com/GitTools/GitVersion/archive/release/4.0.0.zip -O GitVersion.zip
wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -O nuget.exe
unzip GitVersion.zip
mv GitVersion-release-4.0.0/ GitVersion
unzip nunit.zip -d nunit
cd GitVersion/src/
mono ../../nuget.exe restore
xbuild
cd ../../

