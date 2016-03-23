#!/bin/bash
wget https://github.com/GitTools/GitVersion/archive/release/4.0.0.zip -O GitVersion.zip
wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -O nuget.exe
wget http://github.com/nunit/nunitv2/releases/download/2.6.4/NUnit-2.6.4.zip
unzip NUnit-2.6.4.zip
unzip GitVersion.zip
mv GitVersion-release-4.0.0/ GitVersion
unzip nunit.zip -d nunit
cd GitVersion/src/
mono ../../nuget.exe restore
xbuild
cd ../../

