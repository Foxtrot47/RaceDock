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

# The package ships only qualified logo variants, following how Store-published packages are laid out: an
# unqualified Square44x44Logo.png is a neutral fallback Windows may compose over the manifest background, which
# is what drew the accent-coloured plate behind the taskbar icon. Windows resolves the manifest's base names
# against the scale- and targetsize-qualified files below.
Add-Type -AssemblyName System.Drawing
$masterPath = Join-Path $root 'RaceDock/Assets/AppIcon/racedock-256.png'
$master = [System.Drawing.Image]::FromFile($masterPath)
# Every size Windows can ask for at 100-400% scale, with the theme and contrast variations it may request.
$targetSizes = 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 90, 96, 108, 120, 128, 144, 160, 192, 216, 256, 288,
    320, 384, 512, 768, 1024
$variations = '', '_altform-unplated', '_altform-lightunplated', '_contrast-black', '_contrast-white',
    '_altform-unplated_contrast-black', '_altform-unplated_contrast-white', '_altform-lightunplated_contrast-black',
    '_altform-lightunplated_contrast-white'
$scales = 100, 125, 150, 200, 400
$contrast = '', '_contrast-black', '_contrast-white'
function Save-IconPng([System.Drawing.Image] $source, [int] $size, [string] $path) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage($source, 0, 0, $size, $size)
    }
    finally { $graphics.Dispose() }
    try { $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
}
# makeappx validates the manifest's base file names, so the unqualified files must exist as transparent art even
# though every UI surface resolves a qualified variant instead.
Copy-Item (Join-Path $root 'RaceDock/Assets/AppIcon/racedock-44.png') (Join-Path $stage 'Assets/Square44x44Logo.png') -Force
Copy-Item (Join-Path $root 'RaceDock/Assets/AppIcon/racedock-150.png') (Join-Path $stage 'Assets/Square150x150Logo.png') -Force
Copy-Item (Join-Path $root 'RaceDock/Assets/AppIcon/racedock-64.png') (Join-Path $stage 'Assets/StoreLogo.png') -Force
try {
    foreach ($size in $targetSizes) {
        foreach ($variation in $variations) {
            Save-IconPng $master $size (Join-Path $stage "Assets/Square44x44Logo.targetsize-$size$variation.png")
        }
    }
    foreach ($scale in $scales) {
        foreach ($suffix in $contrast) {
            Save-IconPng $master ([int](44 * $scale / 100)) (Join-Path $stage "Assets/Square44x44Logo.scale-$scale$suffix.png")
            Save-IconPng $master ([int](150 * $scale / 100)) (Join-Path $stage "Assets/Square150x150Logo.scale-$scale$suffix.png")
            Save-IconPng $master ([int](50 * $scale / 100)) (Join-Path $stage "Assets/StoreLogo.scale-$scale$suffix.png")
        }
    }
}
finally { $master.Dispose() }
$manifest = (Get-Content (Join-Path $root 'RaceDock.Package/Package.appxmanifest.template') -Raw).Replace('__VERSION__', $packageVersion)
$manifestDocument = [xml]$manifest
Assert-StoreIdentity $manifestDocument.Package.Identity 'Package manifest'

Set-Content (Join-Path $stage 'AppxManifest.xml') $manifest -Encoding utf8

$makeAppx = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\makeappx.exe') | Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $makeAppx) { throw 'Windows SDK makeappx.exe was not found.' }
$package = Join-Path $output "RaceDock-$version-x64.msix"
& $makeAppx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

# The release workflow hashes on a GitHub runner; this local script cannot assume the same cmdlet set, so hash
# the package with the runtime instead of relying on Get-FileHash being available.
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $stream = [System.IO.File]::OpenRead($package)
    try { $hash = ([System.BitConverter]::ToString($sha256.ComputeHash($stream)) -replace '-', '').ToLowerInvariant() }
    finally { $stream.Dispose() }
}
finally { $sha256.Dispose() }
Set-Content "$package.sha256" "$hash  $(Split-Path $package -Leaf)" -Encoding ascii
Write-Host "Created $package"
Write-Host "Package identity: $expectedPackageFamilyName / $packageVersion"
