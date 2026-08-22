[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = 'artifacts/msix'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionFile = [xml](Get-Content (Join-Path $root 'Version.props') -Raw)
$version = [string]$versionFile.Project.PropertyGroup.VersionPrefix
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw "Version.props must contain a stable SemVer VersionPrefix; found '$version'."
}
$packageVersion = "$version.0"

$work = Join-Path $root 'artifacts/msix-work'
$publish = Join-Path $work 'publish'
$stage = Join-Path $work 'package'
$output = Join-Path $root $OutputDirectory
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publish -ItemType Directory -Force | Out-Null
New-Item (Join-Path $stage 'Assets') -ItemType Directory -Force | Out-Null
New-Item $output -ItemType Directory -Force | Out-Null

$publishArgs = @(
    'publish', (Join-Path $root 'RaceDock/RaceDock.csproj'),
    '-c', $Configuration,
    '-r', 'win-x64',
    '--self-contained', 'true',
    '--no-restore',
    '-o', $publish,
    '-p:PublishSingleFile=false',
    '-p:PublishTrimmed=false',
    '-p:PublishReadyToRun=false',
    '-p:DebugSymbols=false',
    '-p:DebugType=None',
    "-p:Version=$version",
    "-p:InformationalVersion=$version"
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Copy-Item (Join-Path $publish '*') $stage -Recurse -Force
Copy-Item (Join-Path $root 'RaceDock/Assets/AppIcon/racedock-44.png') (Join-Path $stage 'Assets/Square44x44Logo.png') -Force
Copy-Item (Join-Path $root 'RaceDock/Assets/AppIcon/racedock-150.png') (Join-Path $stage 'Assets/Square150x150Logo.png') -Force
$manifest = (Get-Content (Join-Path $root 'RaceDock.Package/Package.appxmanifest.template') -Raw).Replace('__VERSION__', $packageVersion)
Set-Content (Join-Path $stage 'AppxManifest.xml') $manifest -Encoding utf8

$makeAppx = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\makeappx.exe') | Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $makeAppx) { throw 'Windows SDK makeappx.exe was not found.' }
$package = Join-Path $output "RaceDock-$version-x64.msix"
& $makeAppx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

$hash = (Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$package.sha256" "$hash  $(Split-Path $package -Leaf)" -Encoding ascii
Write-Host "Created $package"
Write-Host "Package identity: Foxtrot47.RaceDock / $packageVersion"
