[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = 'artifacts/msix'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedPackageName = 'Foxtrot47.RaceDock'
$expectedPublisher = 'CN=B3628FD3-BCE4-4EF1-ADE8-7B0F73A4FC3F'
$expectedPackageFamilyName = 'Foxtrot47.RaceDock_t4mcqp6q26c0g'

function Assert-StoreIdentity($Identity, [string] $Source) {
    if ($Identity.Name -ne $expectedPackageName -or $Identity.Publisher -ne $expectedPublisher) {
        throw "$Source identity must match the reserved Microsoft Store identity."
    }
}

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
    '-o', $publish,
    '-p:RaceDockDistributionBuild=true',
    '-p:RaceDockPackagedBuild=true',
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
foreach ($targetSize in 16, 24, 32, 48, 256) {
    Copy-Item (Join-Path $root "RaceDock/Assets/AppIcon/racedock-$targetSize.png") `
        (Join-Path $stage "Assets/Square44x44Logo.targetsize-$targetSize`_altform-unplated.png") -Force
}
$manifest = (Get-Content (Join-Path $root 'RaceDock.Package/Package.appxmanifest.template') -Raw).Replace('__VERSION__', $packageVersion)
$manifestDocument = [xml]$manifest
Assert-StoreIdentity $manifestDocument.Package.Identity 'Package manifest'

Set-Content (Join-Path $stage 'AppxManifest.xml') $manifest -Encoding utf8

$makeAppx = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\makeappx.exe') | Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $makeAppx) { throw 'Windows SDK makeappx.exe was not found.' }
$package = Join-Path $output "RaceDock-$version-x64.msix"
& $makeAppx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

$hash = (Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$package.sha256" "$hash  $(Split-Path $package -Leaf)" -Encoding ascii
Write-Host "Created $package"
Write-Host "Package identity: $expectedPackageFamilyName / $packageVersion"
