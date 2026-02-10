#!/usr/bin/env pwsh
# NuGet Package Publishing Script for NTG.Adk
# Copyright 2025 NTG - Licensed under Apache License, Version 2.0

# Define all projects to pack (in dependency order)
$projects = @(
    ".\src\NTG.Adk.Boundary\NTG.Adk.Boundary.csproj",
    ".\src\NTG.Adk.CoreAbstractions\NTG.Adk.CoreAbstractions.csproj",
    ".\src\NTG.Adk.Implementations\NTG.Adk.Implementations.csproj",
    ".\src\NTG.Adk.Operators\NTG.Adk.Operators.csproj",
    ".\src\NTG.Adk.Bootstrap\NTG.Adk.Bootstrap.csproj"
)

# Read version from Bootstrap project
$bootstrapPath = ".\src\NTG.Adk.Bootstrap\NTG.Adk.Bootstrap.csproj"
if (-not (Test-Path $bootstrapPath)) {
    Write-Host "ERROR: Could not find $bootstrapPath" -ForegroundColor Red
    exit 1
}

[xml]$csprojXml = Get-Content $bootstrapPath
$version = $csprojXml.Project.PropertyGroup.Version | Where-Object { $_ -ne $null } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version)) {
    Write-Host "ERROR: Could not extract version from $csprojPath" -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NTG.Adk NuGet Package Publisher" -ForegroundColor Cyan
Write-Host "  Version: $version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean old packages
Write-Host "Step 1: Cleaning old packages..." -ForegroundColor Cyan
if (Test-Path ".\nupkgs") {
    Remove-Item ".\nupkgs\*.nupkg" -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Old packages removed" -ForegroundColor Green
} else {
    New-Item -ItemType Directory -Path ".\nupkgs" -Force | Out-Null
    Write-Host "  ✓ Created nupkgs directory" -ForegroundColor Green
}
Write-Host ""

# Step 2: Clean solution
Write-Host "Step 2: Cleaning solution..." -ForegroundColor Cyan
$cleanOutput = dotnet clean --configuration Release --nologo 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Solution cleaned" -ForegroundColor Green
} else {
    Write-Host "  ⚠ Clean warning (continuing...)" -ForegroundColor Yellow
}
Write-Host ""

# Step 3: Build solution
Write-Host "Step 3: Building solution (Release mode)..." -ForegroundColor Cyan
$buildOutput = dotnet build --configuration Release --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Build FAILED!" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Build succeeded" -ForegroundColor Green
Write-Host ""

# Step 4: Pack all 5 NuGet packages
Write-Host "Step 4: Packing NuGet packages (all 5 layers)..." -ForegroundColor Cyan
$packFailed = $false
foreach ($proj in $projects) {
    $projName = Split-Path $proj -Leaf
    Write-Host "  Packing $projName..." -ForegroundColor Gray
    $packOutput = dotnet pack $proj --configuration Release --output .\nupkgs --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    ✗ Pack FAILED for $projName!" -ForegroundColor Red
        Write-Host $packOutput -ForegroundColor Red
        $packFailed = $true
    } else {
        Write-Host "    ✓ $projName packed" -ForegroundColor Green
    }
}
if ($packFailed) {
    exit 1
}
Write-Host "  ✓ All packages packed successfully" -ForegroundColor Green
Write-Host ""

# Step 5: Verify all 5 packages
Write-Host "Step 5: Verifying packages..." -ForegroundColor Cyan
$expectedPackages = @(
    "NTG.Adk.Boundary.$version.nupkg",
    "NTG.Adk.CoreAbstractions.$version.nupkg",
    "NTG.Adk.Implementations.$version.nupkg",
    "NTG.Adk.Operators.$version.nupkg",
    "NTG.Adk.$version.nupkg"
)

$allFound = $true
$packages = @()
Write-Host "  Found packages to publish:" -ForegroundColor Green
foreach ($pkg in $expectedPackages) {
    $pkgPath = ".\nupkgs\$pkg"
    if (Test-Path $pkgPath) {
        $size = (Get-Item $pkgPath).Length
        $sizeKB = [math]::Round($size / 1KB, 2)
        Write-Host "  ✓ $pkg ($sizeKB KB)" -ForegroundColor White
        $packages += $pkg
    } else {
        Write-Host "  ✗ $pkg (NOT FOUND)" -ForegroundColor Red
        $allFound = $false
    }
}

if (-not $allFound) {
    Write-Host "  ✗ ERROR: Some packages not found!" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 6: Get API Key
Write-Host "Step 6: Authenticating..." -ForegroundColor Cyan

$apiKeyPlainText = $env:NTG_ADK_NUGET_API_KEY

if (-not [string]::IsNullOrWhiteSpace($apiKeyPlainText)) {
    Write-Host "  ✓ Found API Key in environment variable 'NTG_ADK_NUGET_API_KEY'" -ForegroundColor Green
} else {
    # Prompt for API Key
    Write-Host "Please enter your NuGet.org API Key:" -ForegroundColor Yellow
    Write-Host "(Get your API key from https://www.nuget.org/account/apikeys)" -ForegroundColor Gray
    $apiKey = Read-Host -AsSecureString

    # Convert SecureString to plain text for dotnet command
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey)
    $apiKeyPlainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)

    if ([string]::IsNullOrWhiteSpace($apiKeyPlainText)) {
        Write-Host "ERROR: API Key cannot be empty!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "API Key received (length: $($apiKeyPlainText.Length) characters)" -ForegroundColor Green
    Write-Host ""
}

# Confirmation
Write-Host "WARNING: This will publish 5 packages (NTG.Adk + 4 layers) version $version to NuGet.org" -ForegroundColor Yellow
Write-Host "Target: https://api.nuget.org/v3/index.json" -ForegroundColor Yellow
Write-Host ""
$confirmation = Read-Host "Are you sure you want to continue? (yes/no)"

if ($confirmation -ne "yes") {
    Write-Host "Publishing cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Starting Publication Process" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0
$publishedPackages = @()
$failedPackages = @()

foreach ($pkg in $packages) {
    $pkgPath = ".\nupkgs\$pkg"
    Write-Host "Publishing $pkg..." -ForegroundColor Cyan

    try {
        $output = dotnet nuget push $pkgPath `
            --api-key $apiKeyPlainText `
            --source https://api.nuget.org/v3/index.json `
            --skip-duplicate `
            2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ SUCCESS: $pkg published" -ForegroundColor Green
            $successCount++
            $publishedPackages += $pkg
        } else {
            # Check if it's a duplicate error
            if ($output -match "already exists" -or $output -match "409") {
                Write-Host "  ⚠ SKIPPED: $pkg already exists on NuGet.org" -ForegroundColor Yellow
                $successCount++
                $publishedPackages += $pkg
            } else {
                Write-Host "  ✗ FAILED: $pkg" -ForegroundColor Red
                Write-Host "  Error: $output" -ForegroundColor Red
                $failCount++
                $failedPackages += $pkg
            }
        }
    } catch {
        Write-Host "  ✗ FAILED: $pkg" -ForegroundColor Red
        Write-Host "  Exception: $_" -ForegroundColor Red
        $failCount++
        $failedPackages += $pkg
    }

    Write-Host ""
}

# Clear API key from memory
$apiKeyPlainText = $null
[System.GC]::Collect()

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Publication Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($successCount -eq 5) {
    Write-Host "🎉 All 5 packages published successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "View your packages at:" -ForegroundColor Cyan
    Write-Host "  https://www.nuget.org/packages/NTG.Adk/" -ForegroundColor White
    Write-Host "  https://www.nuget.org/packages/NTG.Adk.Boundary/" -ForegroundColor White
    Write-Host "  https://www.nuget.org/packages/NTG.Adk.CoreAbstractions/" -ForegroundColor White
    Write-Host "  https://www.nuget.org/packages/NTG.Adk.Implementations/" -ForegroundColor White
    Write-Host "  https://www.nuget.org/packages/NTG.Adk.Operators/" -ForegroundColor White
    Write-Host ""
    Write-Host "Install with:" -ForegroundColor Cyan
    Write-Host "  dotnet add package NTG.Adk --version $version" -ForegroundColor White
    Write-Host ""
    exit 0
} elseif ($successCount -gt 0) {
    Write-Host "⚠ Partial success: $successCount of 5 packages published." -ForegroundColor Yellow
    Write-Host "Please check the errors above for failed packages." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "❌ All packages failed to publish." -ForegroundColor Red
    Write-Host "Please check the errors above and try again." -ForegroundColor Red
    exit 1
}
