# Notes for building Costura on Linux

Make sure:
* To use GitVersion v4 or higher; at the time the porting was done there was no NuGet package for the v4 branch so you'll need to manually replace the files
* To use the correct version of the native LibGit versions that are embedded in Stamp.Fody; libgit2-a99f33e.so appears to work while libgit2-785d8c4.so caused as SIGSEGV in git_index_read (on Ubuntu)
* If you want to debug the tests using MonoDevelop on Linux, you need MonoDevelop 6 - which means you'll be building from master

