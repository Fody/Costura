#!/bin/bash
rm -rf GitVersion
rm -rf Stamp
rm -rf nunit
rm -f nunit.zip
rm -f Stamp.zip
rm -f GitVersion.zip
wget https://github.com/quamotion/Stamp/archive/features/managed-verpatch.zip -O Stamp.zip
wget https://github.com/GitTools/GitVersion/archive/release/4.0.0.zip -O GitVersion.zip
wget https://github.com/nunit/nunit/releases/download/3.2.0/NUnit-3.2.0.zip -O nunit.zip
wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -O nuget.exe
unzip Stamp.zip
mv Stamp-features-managed-verpatch Stamp
unzip GitVersion.zip
mv GitVersion-release-4.0.0/ GitVersion
unzip nunit.zip -d nunit
cd GitVersion/src/
mono ../../nuget.exe restore
xbuild
cd ../../
cd Stamp
mono ../nuget.exe restore
xbuild
cd ..

