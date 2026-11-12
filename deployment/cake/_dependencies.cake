#addin "nuget:?package=Cake.Docker&version=1.5.0"
#addin "nuget:?package=Cake.FileHelpers&version=9.0.0"
#addin "nuget:?package=Cake.Git&version=5.0.1"
#addin "nuget:?package=Cake.GitHub&version=1.0.0"
#addin "nuget:?package=Cake.Issues&version=6.1.0"
#addin "nuget:?package=Cake.Issues.MsBuild&version=6.1.0"
#addin "nuget:?package=Cake.MicrosoftTeams&version=6.0.0"
#addin "nuget:?package=Cake.Sonar&version=6.0.1"
#addin "nuget:?package=LibGit2Sharp&version=0.32.0"
#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Newtonsoft.Json&version=13.0.4"
#addin "nuget:?package=Octokit&version=14.0.0"
#addin "nuget:?package=System.Configuration.ConfigurationManager&version=11.0.*"

#tool "dotnet:?package=GitVersion.Tool&version=6.8.2"
#tool "nuget:?package=MSBuild.Extension.Pack&version=1.9.1"
#tool "nuget:?package=NuGet.CommandLine&version=8.0.0"

public static class BuildDependencies
{
	public const string AzureSignTool = "dotnet:?package=AzureSignTool&version=7.0.1";
	public const string AzureStorageSyncTool = "nuget:?package=AzureStorageSync&version=2.0.0-alpha0039&prerelease";
	public const string GitReleaseManagerTool = "nuget:?package=gitreleasemanager&version=0.20.0";
	public const string JiraCliTool = "nuget:?package=JiraCli&version=1.3.0-alpha0338&prerelease";
	public const string NUnitConsoleRunnerTool = "nuget:?package=NUnit.ConsoleRunner&version=3.22.0";
	public const string NuGetKeyVaultSignTool = "dotnet:?package=NuGetKeyVaultSignTool&version=3.2.3";
	public const string DotNetSonarScannerTool = "nuget:?package=dotnet-sonarscanner&version=12.1.0";
	public const string VpkTool = "dotnet:?package=vpk&version=1.2.161";
}
