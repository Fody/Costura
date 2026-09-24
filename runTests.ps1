param(
    # only run the specified target framework
    [string]$Framework
)

$ErrorActionPreference = 'Stop'

# TUnit test projects are executables. They are run with 'dotnet run' for each target framework.
$projects = @(
    'src\Costura.Fody.Tests\Costura.Fody.Tests.csproj'
)

$failed = @()
foreach ($project in $projects) {
    $frameworks = (dotnet msbuild $project -getProperty:TargetFrameworks -p:Configuration=Release).Trim().Split(';')
    if ($Framework) {
        $frameworks = $frameworks | Where-Object { $_ -eq $Framework }
    }
    foreach ($targetFramework in $frameworks) {
        Write-Host "Running $project ($targetFramework)"
        dotnet run --project $project -c Release -f $targetFramework --no-build
        if ($LASTEXITCODE -ne 0) {
            $failed += "$project ($targetFramework)"
        }
    }
}

if ($failed.Count -gt 0) {
    Write-Host "Failed test runs:`n$($failed -join "`n")"
    exit 1
}
