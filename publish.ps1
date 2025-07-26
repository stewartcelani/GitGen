#!/usr/bin/env pwsh

# GitGen Enhanced Publish Script
# Publishes GitGen for all platforms, organizes into versioned folders, and creates ZIP archives.

param(
# The default value is set after this block to allow for dynamic path resolution.
    [string]$OutputPath = ""
)

# --- Set dynamic default for OutputPath if not provided ---
if (-not $PSBoundParameters.ContainsKey('OutputPath')) {
    $ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $OutputPath = Join-Path $ScriptDirectory "dist"
}

# Define supported runtime identifiers
$SupportedRuntimes = @(
    @{ RID = "win-x64"; Name = "Windows x64"; Extension = ".exe" },
    @{ RID = "linux-x64"; Name = "Linux x64"; Extension = "" },
    @{ RID = "osx-x64"; Name = "macOS x64"; Extension = "" },
    @{ RID = "osx-arm64"; Name = "macOS ARM64"; Extension = "" }
)

function Get-ProjectVersion {
    param([string]$CsprojPath)
    try {
        $versionLine = Get-Content $CsprojPath | Select-String -Pattern '<Version>(.*)</Version>'
        if ($versionLine) {
            return $versionLine.Matches.Groups[1].Value.Trim()
        }
    } catch {}
    Write-Host "⚠️ Could not read version from .csproj, defaulting to 0.0.0" -ForegroundColor Yellow
    return "0.0.0"
}

function Get-FileSize {
    param([string]$Path)
    if (Test-Path $Path) {
        $size = (Get-Item $Path).Length
        if ($size -gt 1MB) {
            return "{0:N1} MB" -f ($size / 1MB)
        } else {
            return "{0:N0} KB" -f ($size / 1KB)
        }
    }
    return "N/A"
}

function Test-PreFlightValidation {
    param([string]$CsprojPath)

    Write-Host "🔍 Running pre-flight validation..." -ForegroundColor Cyan

    # Check if dotnet is available
    Write-Host "   Checking dotnet availability..." -ForegroundColor Gray
    try {
        $dotnetVersion = & dotnet --version 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   ❌ dotnet command failed" -ForegroundColor Red
            return $false
        }
        Write-Host "   ✅ dotnet available (v$dotnetVersion)" -ForegroundColor Green
    } catch {
        Write-Host "   ❌ dotnet command not found: $_" -ForegroundColor Red
        return $false
    }

    # Check if project file exists
    Write-Host "   Checking project file..." -ForegroundColor Gray
    if (!(Test-Path $CsprojPath)) {
        Write-Host "   ❌ Project file not found: $CsprojPath" -ForegroundColor Red
        return $false
    }
    Write-Host "   ✅ Project file exists" -ForegroundColor Green

    # Test version extraction
    Write-Host "   Testing version extraction..." -ForegroundColor Gray
    try {
        $version = Get-ProjectVersion -CsprojPath $CsprojPath
        if ($version -eq "0.0.0") {
            Write-Host "   ⚠️  Version extraction failed, using default" -ForegroundColor Yellow
        } else {
            Write-Host "   ✅ Version extracted: $version" -ForegroundColor Green
        }
    } catch {
        Write-Host "   ❌ Version extraction error: $_" -ForegroundColor Red
        return $false
    }

    # Test build
    Write-Host "   Testing project build..." -ForegroundColor Gray
    try {
        $buildOutput = & dotnet build $CsprojPath -c Release --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Build successful" -ForegroundColor Green
            return $true
        } else {
            Write-Host "   ❌ Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
            Write-Host "   Build output:" -ForegroundColor Red
            Write-Host $buildOutput -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "   ❌ Build error: $_" -ForegroundColor Red
        return $false
    }
}

function Clear-OutputDirectory {
    param([string]$OutputPath)

    if (Test-Path $OutputPath) {
        Write-Host "🧹 Cleaning output directory..." -ForegroundColor Cyan
        Write-Host "   Removing: $OutputPath" -ForegroundColor Gray
        try {
            Remove-Item $OutputPath -Recurse -Force
            Write-Host "   ✅ Output directory cleaned" -ForegroundColor Green
        } catch {
            Write-Host "   ❌ Failed to clean output directory: $_" -ForegroundColor Red
            return $false
        }
    } else {
        Write-Host "🧹 Output directory doesn't exist, skipping clean" -ForegroundColor Gray
    }

    # Create fresh output directory
    try {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
        Write-Host "   ✅ Fresh output directory created" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "   ❌ Failed to create output directory: $_" -ForegroundColor Red
        return $false
    }
}

function Publish-Runtime {
    param(
        [string]$RuntimeId,
        [string]$RuntimeName,
        [string]$Extension,
        [string]$BaseOutputPath,
        [string]$Version
    )

    Write-Host "📦 Publishing $RuntimeName (v$Version, $RuntimeId)..." -ForegroundColor Green

    $ReleaseFolderName = "GitGen-v$Version-$RuntimeId"
    $ReleaseFolderPath = Join-Path $BaseOutputPath $ReleaseFolderName
    $TargetOutputPath = Join-Path $ReleaseFolderPath "GitGen" # Publish into the subfolder

    if (Test-Path $ReleaseFolderPath) {
        Remove-Item $ReleaseFolderPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $TargetOutputPath -Force | Out-Null

    $publishArgs = @(
        "publish",
        "src\GitGen\GitGen.csproj",
        "-c", "Release",
        "-r", $RuntimeId,
        "-o", $TargetOutputPath,
        "--self-contained", "true",
        "/p:PublishSingleFile=true",
        "/p:PublishTrimmed=true",
        "/p:TrimMode=partial"
    )

    try {
        & dotnet $publishArgs | Out-Null

        if ($LASTEXITCODE -eq 0) {
            $executableName = "gitgen$Extension"
            $executablePath = Join-Path $TargetOutputPath $executableName
            $fileSize = Get-FileSize $executablePath

            Write-Host "   ✅ Success: $executablePath" -ForegroundColor Green
            Write-Host "   📏 Size: $fileSize" -ForegroundColor Cyan

            if (Test-Path $executablePath) {
                $canTest = $false
                if ($RuntimeId.StartsWith("win") -and $IsWindows) {
                    $canTest = $true
                } elseif ($RuntimeId.StartsWith("linux") -and $IsLinux) {
                    $canTest = $true
                } elseif ($RuntimeId.StartsWith("osx") -and $IsMacOS) {
                    # macOS architecture compatibility check
                    $currentArch = uname -m
                    if ($currentArch -eq "arm64") {
                        # ARM64 Macs can run both x64 (via Rosetta) and arm64 (native)
                        $canTest = $true
                    } elseif ($RuntimeId -eq "osx-x64") {
                        # Intel Macs can only run x64 binaries
                        $canTest = $true
                    }
                    # Intel Macs cannot run osx-arm64 binaries, so $canTest stays false
                }

                if ($canTest) {
                    Write-Host "   🧪 Testing executable..." -ForegroundColor Yellow
                    try {
                        $testResult = & $executablePath --version 2>&1
                        if ($LASTEXITCODE -eq 0 -and $testResult) {
                            Write-Host "   ✅ Executable test passed: $testResult" -ForegroundColor Green
                        } else {
                            Write-Host "   ⚠️  Executable test failed" -ForegroundColor Yellow
                        }
                    } catch {
                        Write-Host "   ⚠️  Could not test executable: $_" -ForegroundColor Yellow
                    }
                } else {
                    Write-Host "   ⚠️  Cross-platform build (cannot test)" -ForegroundColor Yellow
                }
            }
            return $ReleaseFolderPath
        } else {
            Write-Host "   ❌ Publish failed with exit code: $LASTEXITCODE" -ForegroundColor Red
            return $null
        }
    } catch {
        Write-Host "   ❌ Error during publish: $_" -ForegroundColor Red
        return $null
    }
}

# --- Main execution ---
Write-Host "🚀 GitGen Publisher" -ForegroundColor Magenta
Write-Host "Publishing self-contained, trimmed, single-file executables" -ForegroundColor Gray
Write-Host ""

# Run pre-flight validation
$CsprojPath = "src\GitGen\GitGen.csproj"
if (!(Test-PreFlightValidation -CsprojPath $CsprojPath)) {
    Write-Host ""
    Write-Host "❌ Pre-flight validation failed. Aborting publish." -ForegroundColor Red
    Write-Host "💡 Please fix the issues above and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Clean output directory
if (!(Clear-OutputDirectory -OutputPath $OutputPath)) {
    Write-Host ""
    Write-Host "❌ Failed to clean output directory. Aborting publish." -ForegroundColor Red
    exit 1
}
Write-Host ""

$ProjectVersion = Get-ProjectVersion -CsprojPath $CsprojPath
Write-Host "ℹ️ Detected project version: $ProjectVersion" -ForegroundColor Yellow
Write-Host ""

Write-Host "📋 Publishing for all supported platforms..." -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$totalCount = $SupportedRuntimes.Count
$foldersToZip = @()

foreach ($runtime in $SupportedRuntimes) {
    $folderPath = Publish-Runtime -RuntimeId $runtime.RID -RuntimeName $runtime.Name -Extension $runtime.Extension -BaseOutputPath $OutputPath -Version $ProjectVersion
    if ($folderPath) {
        $successCount++
        $foldersToZip += $folderPath
    }
    Write-Host ""
}

# Copy current platform's build to root for convenience
Write-Host "🚀 Copying current platform's build to root output path for convenience..." -ForegroundColor Magenta
$currentRid = ""
if ($IsWindows) { $currentRid = "win-x64" }
elseif ($IsLinux) { $currentRid = "linux-x64" }
elseif ($IsMacOS) {
    $arch = uname -m
    if ($arch -eq "arm64") { $currentRid = "osx-arm64" } else { $currentRid = "osx-x64" }
}

if ($currentRid) {
    $currentPlatformSourcePath = Join-Path $OutputPath "GitGen-v$ProjectVersion-$currentRid\GitGen"
    if (Test-Path $currentPlatformSourcePath) {
        Write-Host "   Copying from $currentPlatformSourcePath to $OutputPath"
        Copy-Item -Path "$currentPlatformSourcePath\*" -Destination $OutputPath -Recurse -Force
        Write-Host "   ✅ Current platform build copied successfully." -ForegroundColor Green
    } else {
        Write-Host "   ⚠️ Could not find build for current platform ($currentRid) to copy." -ForegroundColor Yellow
    }
}
Write-Host ""

# Zip release artifacts and clean up
if ($foldersToZip.Count -gt 0) {
    Write-Host "📦 Zipping release artifacts..." -ForegroundColor Magenta
    foreach ($folder in $foldersToZip) {
        $zipFileName = "$(Split-Path $folder -Leaf).zip"
        $zipFilePath = Join-Path $OutputPath $zipFileName
        Write-Host "   Creating $zipFilePath..."
        try {
            Compress-Archive -Path "$folder\*" -DestinationPath $zipFilePath -Force
            Write-Host "   ✅ Successfully created zip file." -ForegroundColor Green
        } catch {
            Write-Host "   ❌ Failed to create zip file: $_" -ForegroundColor Red
        }
    }
    Write-Host ""

    Write-Host "🧹 Cleaning up source folders..." -ForegroundColor Magenta
    foreach ($folder in $foldersToZip) {
        Write-Host "   Removing $folder..."
        Remove-Item -Path $folder -Recurse -Force
    }
    Write-Host ""
}

Write-Host "📊 Publish Summary:" -ForegroundColor Magenta
Write-Host "   ✅ Successful: $successCount/$totalCount" -ForegroundColor Green
Write-Host "   📂 Output location: $OutputPath" -ForegroundColor Cyan
Write-Host ""

Write-Host "🎉 GitGen publishing complete!" -ForegroundColor Green
Write-Host ""

Write-Host "💡 Usage Example:" -ForegroundColor Yellow
Write-Host "   Publish for all platforms: .\publish.ps1" -ForegroundColor Gray

exit $($successCount -eq $totalCount ? 0 : 1)