#!/bin/bash

# Clean up files that may have been left over from a previous run
rm -f GitVersion.zip
rm -f nuget.exe
rm -f NUnit-2.6.4.zip
rm -f NUnit.ApplicationDomain.zip
rm -rf nunit.applicationdomain-5.0.2
rm -rf GitVersion
rm -rf NUnit-2.6.4

wget https://github.com/GitTools/GitVersion/archive/release/4.0.0.zip -O GitVersion.zip
wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -O nuget.exe
wget http://github.com/nunit/nunitv2/releases/download/2.6.4/NUnit-2.6.4.zip
wget https://github.com/quamotion/nunit.applicationdomain/archive/v5.0.2.zip -O NUnit.ApplicationDomain.zip
unzip NUnit-2.6.4.zip
unzip NUnit.ApplicationDomain.zip
cd nunit.applicationdomain-5.0.2/src/NUnit.ApplicationDomain/
xbuild
cd ../../../
unzip GitVersion.zip
mv GitVersion-release-4.0.0/ GitVersion
unzip nunit.zip -d nunit
cd GitVersion/src/
mono ../../nuget.exe restore
xbuild
cd ../../

