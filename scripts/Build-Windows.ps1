param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assetManifest = Join-Path $PSScriptRoot 'expected-assets.json'
$assetRoot = Join-Path $root 'Assets'
if (!(Test-Path -LiteralPath $assetManifest -PathType Leaf) -or !(Test-Path -LiteralPath $assetRoot -PathType Container)) {
    throw "Assets are not present. Run powershell -ExecutionPolicy Bypass -File .\scripts\Fetch-Upstream-Assets.ps1 first."
}
dotnet build (Join-Path $root 'SunAlanPet.Windows.csproj') -c $Configuration -r win-x64
dotnet publish (Join-Path $root 'SunAlanPet.Windows.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $root 'publish')
$exe = Join-Path $root 'publish\SunAlanPet.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "Missing published executable: $exe" }
$result = Join-Path $root 'publish\self-test-result.txt'
if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result -Force }
$selfTestArgs = @('--self-test', '--self-test-result', ('"{0}"' -f $result))
$proc = Start-Process -FilePath $exe -ArgumentList $selfTestArgs -Wait -PassThru -WindowStyle Hidden
if ($proc.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)) { throw "Self-test failed (exit $($proc.ExitCode))" }
Get-Content -LiteralPath $result
Write-Output "Published to $(Join-Path $root 'publish')"
