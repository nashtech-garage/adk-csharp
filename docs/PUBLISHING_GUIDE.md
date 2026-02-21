# NTG.Adk Publishing Guide

This document outlines the standard procedure for publishing a new version of the NTG.Adk library. Following these steps ensures consistency, avoids broken builds, and keeps all documentation up-to-date.

## Prerequisites

- You must have the .NET 8 SDK or newer installed (`dotnet --version`).
- You must have an active NuGet API Key.
  - The script will prompt for it if not found in the environment.
  - **Optional but recommended**: Set your API key as an environment variable to bypass the prompt:
    - **Windows**: `setx NTG_ADK_NUGET_API_KEY "your_api_key_here"`
    - **Linux/macOS**: `export NTG_ADK_NUGET_API_KEY="your_api_key_here"`

## Step-by-Step Publishing Process

### 1. Update the Version Number

Before publishing, you must bump the version number across all relevant files.

1. **Update `.csproj` files**
   Change the `<Version>x.x.x</Version>` tag in all 5 project files:
   - `src/NTG.Adk.Boundary/NTG.Adk.Boundary.csproj`
   - `src/NTG.Adk.CoreAbstractions/NTG.Adk.CoreAbstractions.csproj`
   - `src/NTG.Adk.Implementations/NTG.Adk.Implementations.csproj`
   - `src/NTG.Adk.Operators/NTG.Adk.Operators.csproj`
   - `src/NTG.Adk.Bootstrap/NTG.Adk.Bootstrap.csproj`

2. **Update Documentation Headers**
   Search and replace the old version string (e.g., `1.8.9`) with the new version (e.g., `1.8.10`) in the following files:
   - `README.md`
   - `docs/FEATURES.md`
   - `docs/COMPATIBILITY.md`
   - `docs/ARCHITECTURE.md`
   - `docs/GETTING_STARTED.md`
   - `docs/STATUS.md` (Update `**Version**:` and `**Last Updated**:`)
   - `llms-full.txt` (Ensure the AI context file is also updated)

### 2. Update the Changelog

We maintain a clean and concise changelog.

1. Open `docs/CHANGELOG.md`.
2. Add a new Markdown header `## [x.x.x] - YYYY-MM-DD` at the very top.
3. Document the changes concisely, organizing them under relevant sub-headings (e.g., `### 🐛 **BUG FIX**`, `### 🚀 **FEATURE**`).
4. **Archive Check**: If `CHANGELOG.md` is becoming too long, move versions older than the 5 most recent releases into `docs/archives/CHANGELOG_archive.md`. Ensure the footer of the main `CHANGELOG.md` points to the archive file.

### 3. Commit the Changes

Group all version-bump and changelog updates into a single commit:

```bash
git add .
git commit -m "chore: bump version to x.x.x and update documentation"
```

### 4. Run the Publish Script

We use an automated PowerShell script to clean, build, pack, and push all 5 layers to NuGet.org in the correct dependency order.

Run the script from the root of the repository:

```powershell
.\publish-nuget.ps1
```

**What the script does:**
1. **Cleans** the `.\nupkgs` directory.
2. Runs `dotnet clean Release`.
3. Runs `dotnet build Release` to ensure there are no compilation errors.
4. Runs `dotnet pack` on all 5 projects.
5. Verifies all 5 `.nupkg` files were generated successfully.
6. Prompts for your NuGet API Key (if not in the environment variable).
7. Asks for final confirmation before pushing.
8. Pushes the packages to NuGet.org using `dotnet nuget push`.

### 5. Verify the Release

Once the script completes successfully:
1. Verify the packages are listed on [NuGet.org](https://www.nuget.org/profiles/NTG). It may take up to 10 minutes for NuGet to index the new version.
2. Push your final commit and (optionally) tag the release in Git:

```bash
git tag vX.X.X
git push origin master --tags
```
