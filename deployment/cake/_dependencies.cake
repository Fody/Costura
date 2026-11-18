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

#tool "nuget:?package=MSBuild.Extension.Pack&version=1.9.1"
#tool "nuget:?package=NuGet.CommandLine&version=8.0.0"

using System.Text.Json;

public static class BuildDependencies
{
	public const string AzureSignTool = "dotnet:?package=AzureSignTool&version=7.0.1";
	public const string AzureStorageSyncTool = "nuget:?package=AzureStorageSync&version=2.0.0-alpha0039&prerelease";
	public const string GitReleaseManagerTool = "nuget:?package=gitreleasemanager&version=0.20.0";
	public const string GitVersionTool = "dotnet:?package=GitVersion.Tool&version=7.0.0";
	public const string JiraCliTool = "nuget:?package=JiraCli&version=1.3.0-alpha0338&prerelease";
	public const string NUnitConsoleRunnerTool = "nuget:?package=NUnit.ConsoleRunner&version=3.22.0";
	public const string NuGetKeyVaultSignTool = "dotnet:?package=NuGetKeyVaultSignTool&version=3.2.3";
	public const string DotNetSonarScannerTool = "nuget:?package=dotnet-sonarscanner&version=12.1.0";
	public const string VpkTool = "dotnet:?package=vpk&version=1.2.161";

	static BuildDependencies()
	{
		ValidateGitVersionToolVersion();
	}

	private static void ValidateGitVersionToolVersion()
	{
		try
		{
			// Extract version from constant (format: "dotnet:?package=GitVersion.Tool&version=X.Y.Z")
			var constantVersion = ExtractVersion(GitVersionTool);
			if (string.IsNullOrEmpty(constantVersion))
			{
				throw new InvalidOperationException("Failed to extract GitVersion.Tool version from BuildDependencies.GitVersionTool constant");
			}

			// Read version from .config/dotnet-tools.json
			var configPath = System.IO.Path.Combine(".", ".config", "dotnet-tools.json");
			if (!System.IO.File.Exists(configPath))
			{
				throw new FileNotFoundException($"dotnet-tools.json not found at {configPath}");
			}

			var jsonContent = System.IO.File.ReadAllText(configPath);
			using (var jsonDoc = JsonDocument.Parse(jsonContent))
			{
				var root = jsonDoc.RootElement;
				if (!root.TryGetProperty("tools", out var tools) ||
					!tools.TryGetProperty("gitversion.tool", out var gitVersionTool) ||
					!gitVersionTool.TryGetProperty("version", out var versionElement))
				{
					throw new InvalidOperationException("gitversion.tool version not found in dotnet-tools.json");
				}

				var configVersion = versionElement.GetString();
				if (string.IsNullOrEmpty(configVersion))
				{
					throw new InvalidOperationException("gitversion.tool version is empty in dotnet-tools.json");
				}

				if (!string.Equals(constantVersion, configVersion, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException(
						$"GitVersion.Tool version mismatch: BuildDependencies has '{constantVersion}' but .config/dotnet-tools.json has '{configVersion}'. " +
						"Please update both to the same version.");
				}
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to validate GitVersion.Tool version: {ex.Message}", ex);
		}
	}

	private static string ExtractVersion(string packageString)
	{
		// Extract version from "dotnet:?package=GitVersion.Tool&version=X.Y.Z" or "nuget:?package=...&version=X.Y.Z&prerelease"
		var versionPrefix = "&version=";
		var index = packageString.IndexOf(versionPrefix, StringComparison.OrdinalIgnoreCase);
		if (index >= 0)
		{
			var versionStart = index + versionPrefix.Length;
			var nextAmp = packageString.IndexOf('&', versionStart);
			
			if (nextAmp >= 0)
			{
				return packageString.Substring(versionStart, nextAmp - versionStart);
			}
			else
			{
				return packageString.Substring(versionStart);
			}
		}
		
		return null;
	}
}
