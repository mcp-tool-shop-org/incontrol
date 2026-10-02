# Generate-ProofPack.ps1
#
# Creates a Release Artifact Proof Pack containing:
# - Build checksums (SHA256)
# - Dependency manifest (NuGet packages)
# - Git information
# - Build provenance
# - File manifest with sizes
#
# Usage: .\Generate-ProofPack.ps1 -OutputPath ./artifacts -Version 0.9.0-rc.1

param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$PublishPath = "./publish",

    [string]$SolutionPath = "."
)

$ErrorActionPreference = "Stop"

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

$proofPackPath = Join-Path $OutputPath "proof-pack-$Version"
New-Item -ItemType Directory -Force -Path $proofPackPath | Out-Null

Write-Host "Generating Proof Pack for version $Version" -ForegroundColor Cyan

# 1. Generate file checksums
Write-Host "  [1/5] Generating file checksums..." -ForegroundColor Yellow
$checksumFile = Join-Path $proofPackPath "checksums-sha256.txt"

if (Test-Path $PublishPath) {
    Get-ChildItem -Path $PublishPath -Recurse -File | ForEach-Object {
        $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLower()
        $relativePath = $_.FullName.Substring((Get-Item $PublishPath).FullName.Length + 1)
        "$hash  $relativePath" | Out-File -FilePath $checksumFile -Append -Encoding utf8
    }
    Write-Host "    Generated checksums for $(Get-ChildItem -Path $PublishPath -Recurse -File | Measure-Object | Select-Object -ExpandProperty Count) files"
} else {
    Write-Host "    Publish path not found, skipping file checksums" -ForegroundColor DarkYellow
}

# 2. Generate dependency manifest
Write-Host "  [2/5] Generating dependency manifest..." -ForegroundColor Yellow
$dependencyFile = Join-Path $proofPackPath "dependencies.json"

$dependencies = @{
    format = "incontrol-dependencies-v1"
    version = $Version
    generatedAt = (Get-Date -Format 'o')
    packages = @()
}

# Parse all .csproj files for PackageReference
Get-ChildItem -Path $SolutionPath -Recurse -Filter "*.csproj" | ForEach-Object {
    [xml]$csproj = Get-Content $_.FullName
    $projectName = $_.BaseName

    $csproj.SelectNodes("//PackageReference") | ForEach-Object {
        $dependencies.packages += @{
            project = $projectName
            id = $_.Include
            version = $_.Version
        }
    }
}

$dependencies | ConvertTo-Json -Depth 10 | Out-File -FilePath $dependencyFile -Encoding utf8
Write-Host "    Found $($dependencies.packages.Count) package dependencies"

# 3. Generate git information
Write-Host "  [3/5] Generating git information..." -ForegroundColor Yellow
$gitFile = Join-Path $proofPackPath "git-info.json"

$gitInfo = @{
    format = "incontrol-git-v1"
    repository = ""
    commit = ""
    commitShort = ""
    branch = ""
    tag = ""
    author = ""
    timestamp = ""
    dirty = $false
}

try {
    $gitInfo.repository = (git remote get-url origin 2>$null) -replace '\.git$', ''
    $gitInfo.commit = git rev-parse HEAD 2>$null
    $gitInfo.commitShort = git rev-parse --short HEAD 2>$null
    $gitInfo.branch = git rev-parse --abbrev-ref HEAD 2>$null
    $gitInfo.tag = git describe --tags --exact-match 2>$null
    $gitInfo.author = git log -1 --format='%an <%ae>' 2>$null
    $gitInfo.timestamp = git log -1 --format='%aI' 2>$null
    $gitInfo.dirty = (git status --porcelain 2>$null | Measure-Object | Select-Object -ExpandProperty Count) -gt 0
} catch {
    Write-Host "    Git information partially unavailable" -ForegroundColor DarkYellow
}

$gitInfo | ConvertTo-Json -Depth 5 | Out-File -FilePath $gitFile -Encoding utf8
Write-Host "    Commit: $($gitInfo.commitShort)"

# 4. Generate build provenance
Write-Host "  [4/5] Generating build provenance..." -ForegroundColor Yellow
$provenanceFile = Join-Path $proofPackPath "provenance.json"

$provenance = @{
    format = "incontrol-provenance-v1"
    version = $Version
    build = @{
        timestamp = (Get-Date -Format 'o')
        machine = $env:COMPUTERNAME
        user = $env:USERNAME
        os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        dotnet = (dotnet --version 2>$null)
        powershell = $PSVersionTable.PSVersion.ToString()
    }
    environment = @{
        ci = $env:CI -eq "true"
        github_actions = $env:GITHUB_ACTIONS -eq "true"
        azure_pipelines = $null -ne $env:BUILD_BUILDID
        local = -not ($env:CI -eq "true")
    }
    source = @{
        repository = $gitInfo.repository
        commit = $gitInfo.commit
        branch = $gitInfo.branch
        dirty = $gitInfo.dirty
    }
}

# Add CI-specific info
if ($env:GITHUB_ACTIONS -eq "true") {
    $provenance.ci_info = @{
        run_id = $env:GITHUB_RUN_ID
        run_number = $env:GITHUB_RUN_NUMBER
        workflow = $env:GITHUB_WORKFLOW
        actor = $env:GITHUB_ACTOR
        ref = $env:GITHUB_REF
    }
}

$provenance | ConvertTo-Json -Depth 10 | Out-File -FilePath $provenanceFile -Encoding utf8
Write-Host "    Build environment: $(if ($provenance.environment.local) { 'Local' } else { 'CI' })"

# 5. Generate file manifest
Write-Host "  [5/5] Generating file manifest..." -ForegroundColor Yellow
$manifestFile = Join-Path $proofPackPath "file-manifest.json"

$manifest = @{
    format = "incontrol-manifest-v1"
    version = $Version
    generatedAt = (Get-Date -Format 'o')
    files = @()
    statistics = @{
        totalFiles = 0
        totalSizeBytes = 0
        extensions = @{}
    }
}

if (Test-Path $PublishPath) {
    Get-ChildItem -Path $PublishPath -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring((Get-Item $PublishPath).FullName.Length + 1)
        $extension = $_.Extension.ToLower()

        $manifest.files += @{
            path = $relativePath
            size = $_.Length
            extension = $extension
            lastModified = $_.LastWriteTimeUtc.ToString('o')
        }

        $manifest.statistics.totalFiles++
        $manifest.statistics.totalSizeBytes += $_.Length

        if ($manifest.statistics.extensions.ContainsKey($extension)) {
            $manifest.statistics.extensions[$extension]++
        } else {
            $manifest.statistics.extensions[$extension] = 1
        }
    }
}

$manifest | ConvertTo-Json -Depth 10 | Out-File -FilePath $manifestFile -Encoding utf8

$totalSizeMB = [math]::Round($manifest.statistics.totalSizeBytes / 1MB, 2)
Write-Host "    Total files: $($manifest.statistics.totalFiles) ($totalSizeMB MB)"

# Generate summary
Write-Host ""
Write-Host "Proof Pack generated successfully!" -ForegroundColor Green
Write-Host "  Location: $proofPackPath" -ForegroundColor Gray
Write-Host "  Contents:" -ForegroundColor Gray
Get-ChildItem -Path $proofPackPath | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1KB, 2)
    Write-Host "    - $($_.Name) ($sizeMB KB)" -ForegroundColor Gray
}

# Create zip archive
$zipPath = Join-Path $OutputPath "proof-pack-$Version.zip"
Compress-Archive -Path $proofPackPath -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Archive created: $zipPath" -ForegroundColor Cyan
