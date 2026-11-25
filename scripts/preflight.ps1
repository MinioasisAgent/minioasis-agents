# Access: Level 2
# Last Updated: 2026-11-25

# Runs every check a pull request must pass: restore, formatting, tests, publish. Stops at the first failure.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Test-Path 'backend/minioasis.slnx')) {
    $projects = Get-ChildItem -Recurse -Filter '*.csproj' | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' }
    if ($projects) {
        Write-Error 'Projects exist, but backend/minioasis.slnx is missing.'
        exit 1
    }
    Write-Output 'No .NET code yet: nothing to check.'
    exit 0
}

Set-Location backend
dotnet restore minioasis.slnx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet format minioasis.slnx --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test minioasis.slnx --configuration Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish Minioasis.Api/Minioasis.Api.csproj --configuration Release --no-restore
exit $LASTEXITCODE
