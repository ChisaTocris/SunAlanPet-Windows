param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $PSScriptRoot 'expected-assets.json'
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Missing asset hash manifest: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$assetRoot = Join-Path $ProjectRoot 'Assets'

function Get-RelativeAssetPath([string]$basePath, [string]$filePath) {
    return $filePath.Substring($basePath.Length + 1).Replace('\', '/')
}

function Test-AssetTree([string]$basePath) {
    $missing = [System.Collections.Generic.List[string]]::new()
    $mismatched = [System.Collections.Generic.List[string]]::new()
    $actual = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    if (!(Test-Path -LiteralPath $basePath -PathType Container)) {
        return [pscustomobject]@{ Valid = $false; Missing = @('Assets directory'); Mismatched = @(); Extra = @() }
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $basePath -Recurse -File)) {
        [void]$actual.Add((Get-RelativeAssetPath $basePath $file.FullName))
    }

    foreach ($expected in @($manifest.files)) {
        $relative = "$($expected.pet)/$($expected.path)"
        $path = Join-Path $basePath ($relative -replace '/', '\')
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            $missing.Add($relative)
            continue
        }

        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expected.sha256.ToLowerInvariant()) {
            $mismatched.Add("${relative}: $actualHash != $($expected.sha256)")
        }
        [void]$actual.Remove($relative)
    }

    $extra = @($actual | Sort-Object)
    return [pscustomobject]@{
        Valid = ($missing.Count -eq 0 -and $mismatched.Count -eq 0 -and $extra.Count -eq 0)
        Missing = @($missing)
        Mismatched = @($mismatched)
        Extra = $extra
    }
}

function Write-AssetCheckFailure($check, [string]$context) {
    $parts = @()
    if ($check.Missing.Count) { $parts += "missing: $($check.Missing -join ', ')" }
    if ($check.Mismatched.Count) { $parts += "hash mismatch: $($check.Mismatched -join '; ')" }
    if ($check.Extra.Count) { $parts += "unexpected files: $($check.Extra -join ', ')" }
    throw "$context ($($parts -join ' | '))"
}

function Assert-ChildPath([string]$childPath, [string]$parentPath, [string]$label) {
    $childFull = [System.IO.Path]::GetFullPath($childPath).TrimEnd('\')
    $parentFull = [System.IO.Path]::GetFullPath($parentPath).TrimEnd('\')
    if (!$childFull.StartsWith($parentFull + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$label is outside its allowed parent: $childFull"
    }
}

if (Test-Path -LiteralPath $assetRoot -PathType Container) {
    $existing = Test-AssetTree $assetRoot
    if ($existing.Valid) {
        Write-Output 'Assets already exist and match the pinned upstream SHA256 manifest; nothing to download.'
        exit 0
    }

    Write-AssetCheckFailure $existing 'Existing Assets are incomplete or differ from the pinned upstream content. No files were changed; repair or remove Assets and run again.'
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SunAlanPet-assets-" + [guid]::NewGuid().ToString('N'))
$projectStaging = Join-Path $ProjectRoot (".assets-staging-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $sourceRoot = Join-Path $tempRoot 'source'
    New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null

    foreach ($repo in @($manifest.repositories)) {
        $archive = Join-Path $tempRoot ("$($repo.name).zip")
        $extractRoot = Join-Path $tempRoot $repo.name
        $url = "https://codeload.github.com/$($repo.owner)/$($repo.name)/zip/$($repo.commit)"
        Write-Output "Downloading $($repo.owner)/$($repo.name) at $($repo.commit)..."
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
        Expand-Archive -LiteralPath $archive -DestinationPath $extractRoot -Force

        $expectedSource = Join-Path $extractRoot ("$($repo.name)-$($repo.commit)")
        if (!(Test-Path -LiteralPath $expectedSource -PathType Container)) {
            throw "Downloaded archive root was not the pinned commit directory: $expectedSource"
        }

        $sourceAssets = Join-Path $expectedSource 'Assets'
        if (!(Test-Path -LiteralPath $sourceAssets -PathType Container)) {
            throw "Downloaded repository has no Assets directory: $sourceAssets"
        }
        Copy-Item -LiteralPath $sourceAssets -Destination (Join-Path $sourceRoot $repo.name) -Recurse -Force
    }

    $staging = Join-Path $tempRoot 'Assets'
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    foreach ($pet in @($manifest.pets)) {
        $source = Join-Path $sourceRoot $pet.name
        $destination = Join-Path $staging $pet.name
        New-Item -ItemType Directory -Path (Join-Path $destination 'Clips') -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $source 'face.png') -Destination $destination -Force
        Copy-Item -LiteralPath (Join-Path $source 'clips.json') -Destination $destination -Force
        foreach ($clip in @($manifest.files | Where-Object { $_.pet -eq $pet.name -and $_.path -like 'Clips/*' })) {
            $relative = $clip.path -replace '/', '\'
            $destinationPath = Join-Path (Join-Path $staging $pet.name) $relative
            Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destinationPath -Force
        }
    }

    $staged = Test-AssetTree $staging
    if (!$staged.Valid) {
        Write-AssetCheckFailure $staged 'Downloaded assets failed the pinned SHA256 manifest. No files were installed.'
    }

    if (Test-Path -LiteralPath $assetRoot) {
        throw "Assets appeared while downloading; refusing to overwrite $assetRoot"
    }

    Copy-Item -LiteralPath $staging -Destination $projectStaging -Recurse -Force
    $projectStaged = Test-AssetTree $projectStaging
    if (!$projectStaged.Valid) {
        Write-AssetCheckFailure $projectStaged 'Project staging assets failed the SHA256 manifest. No assets were installed.'
    }
    Move-Item -LiteralPath $projectStaging -Destination $assetRoot

    $installedCheck = Test-AssetTree $assetRoot
    if (!$installedCheck.Valid) {
        Write-AssetCheckFailure $installedCheck 'Installed assets failed the post-install SHA256 manifest.'
    }
    Write-Output "Installed and verified $($manifest.files.Count) asset files under $assetRoot."
}
catch {
    if (Test-Path -LiteralPath $projectStaging) {
        try {
            Assert-ChildPath $projectStaging $ProjectRoot 'Project staging directory'
            Remove-Item -LiteralPath $projectStaging -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Could not clean project staging directory safely: $($_.Exception.Message)"
        }
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        try {
            Assert-ChildPath $tempRoot ([System.IO.Path]::GetTempPath()) 'Temporary download directory'
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Could not clean temporary download directory safely: $($_.Exception.Message)"
        }
    }
}
