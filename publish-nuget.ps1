#!/usr/bin/env pwsh
# NuGet Package Publishing Script for NTG.Adk
# Copyright 2025 NTG - Licensed under Apache License, Version 2.0

# Read version from .csproj file
$csprojPath = ".\src\NTG.Adk.Bootstrap\NTG.Adk.Bootstrap.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Host "ERROR: Could not find $csprojPath" -ForegroundColor Red
    exit 1
}

[xml]$csprojXml = Get-Content $csprojPath
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

# Step 4: Pack NuGet package (Bootstrap only)
Write-Host "Step 4: Packing NuGet package (NTG.Adk)..." -ForegroundColor Cyan
$packOutput = dotnet pack $csprojPath --configuration Release --output .\nupkgs --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Pack FAILED!" -ForegroundColor Red
    Write-Host $packOutput -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Packages packed successfully" -ForegroundColor Green
Write-Host ""

# Step 5: Verify package
Write-Host "Step 5: Verifying package..." -ForegroundColor Cyan
$packages = Get-ChildItem ".\nupkgs\NTG.Adk.$version.nupkg" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name

if ($packages.Count -eq 0) {
    Write-Host "  ✗ ERROR: NTG.Adk package not found!" -ForegroundColor Red
    exit 1
}

Write-Host "  Found package to publish:" -ForegroundColor Green
foreach ($pkg in $packages) {
    $pkgPath = ".\nupkgs\$pkg"
    if (Test-Path $pkgPath) {
        $size = (Get-Item $pkgPath).Length
        $sizeKB = [math]::Round($size / 1KB, 2)
        Write-Host "  ✓ $pkg ($sizeKB KB)" -ForegroundColor White
    } else {
        Write-Host "  ✗ $pkg (NOT FOUND)" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

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

# Confirmation
Write-Host "WARNING: This will publish NTG.Adk $version to NuGet.org" -ForegroundColor Yellow
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

if ($successCount -eq 1) {
    Write-Host "🎉 Package published successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "View your packages at:" -ForegroundColor Cyan
    Write-Host "  https://www.nuget.org/packages/NTG.Adk/" -ForegroundColor White
    Write-Host ""
    Write-Host "Install with:" -ForegroundColor Cyan
    Write-Host "  dotnet add package NTG.Adk --version $version" -ForegroundColor White
    Write-Host ""
    exit 0
} else {
    Write-Host "❌ Package failed to publish." -ForegroundColor Red
    Write-Host "Please check the errors above and try again." -ForegroundColor Red
    exit 1
}
