[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v.+')]
    [string]$Tag,

    [string]$CommitMessage = "",

    [string]$Remote = "origin",

    [switch]$SkipGit,

    [switch]$Yes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$ProjectPath = Join-Path $RepoRoot "src\TerminalShell\TerminalShell.csproj"
$TestProjectPath = Join-Path $RepoRoot "src\TerminalShell.Tests\TerminalShell.Tests.csproj"
$BasePublishRelative = "src\TerminalShell\bin\Release\net8.0-windows\win-x64"
$FrameworkPublishRelative = Join-Path $BasePublishRelative "publish_single_file"
$SelfContainedPublishRelative = Join-Path $BasePublishRelative "publish_single_file_self_contained"
$FrameworkPublishDir = Join-Path $RepoRoot $FrameworkPublishRelative
$SelfContainedPublishDir = Join-Path $RepoRoot $SelfContainedPublishRelative

if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
    $CommitMessage = "Release $Tag"
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

function Resolve-RepoPath {
    param([string]$RelativePath)

    $path = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $RelativePath))
    $rootWithSeparator = $RepoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not ($path.Equals($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
              $path.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to use path outside repository root: $path"
    }

    return $path
}

function Remove-DirectoryInsideRepo {
    param([string]$RelativePath)

    $path = Resolve-RepoPath $RelativePath
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
        Write-Host "Removed $RelativePath"
    }
}

function Assert-FileExists {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Expected file was not generated: $Path"
    }
}

function Assert-GitAvailable {
    Invoke-Checked "git" @("rev-parse", "--is-inside-work-tree")
    $currentBranch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($currentBranch)) {
        throw "Unable to determine current Git branch."
    }

    if ($currentBranch -ne "main") {
        throw "Release script expects to run on branch 'main'. Current branch: '$currentBranch'."
    }

    & git remote get-url $Remote *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Git remote '$Remote' was not found."
    }

    & git rev-parse -q --verify "refs/tags/$Tag" *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "Local tag already exists: $Tag"
    }

    $remoteTag = & git ls-remote --tags $Remote "refs/tags/$Tag"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to check remote tag '$Tag' on remote '$Remote'."
    }

    if ($remoteTag) {
        throw "Remote tag already exists: $Tag"
    }
}

function Test-ForbiddenGitPath {
    param([string]$Path)

    $normalized = $Path -replace '\\', '/'
    $forbiddenPatterns = @(
        '^(specs\.md|todo\.md)$',
        '^\.agent/',
        '^work_home/',
        '^work_service4app/',
        '(^|/)bin/',
        '(^|/)obj/',
        '\.(exe|dll|pdb|zip|7z|rar|msi)$',
        '(^|/)config\.json$',
        '(^|/)config_draft\.json$',
        '(^|/)config_bak/',
        '(^|/)history/',
        '(^|/)debug_html/',
        '(^|/)debug_output\.log$'
    )

    foreach ($pattern in $forbiddenPatterns) {
        if ($normalized -match $pattern) {
            return $true
        }
    }

    return $false
}

function Invoke-GitRelease {
    Write-Step "Validating Git state"
    Assert-GitAvailable

    $candidatePaths = @("README.md", ".gitignore", "LICENSE", "docs", "src", "scripts", ".github")
    $candidateStatus = @(& git status --short -- $candidatePaths)

    Write-Step "Candidate source changes"
    if ($candidateStatus.Count -eq 0) {
        Write-Host "No candidate source changes detected. The script will tag the current HEAD."
    } else {
        $candidateStatus | ForEach-Object { Write-Host $_ }
    }

    if (-not $Yes) {
        Write-Host ""
        Write-Host "This will commit candidate source changes, create tag '$Tag', push branch 'main', and push the tag to '$Remote'."
        $confirmation = Read-Host "Type '$Tag' to continue"
        if ($confirmation -ne $Tag) {
            throw "Release cancelled by user."
        }
    }

    Write-Step "Staging source files"
    Invoke-Checked "git" @("add", "--", "README.md", ".gitignore", "LICENSE", "docs", "src", "scripts", ".github")

    $stagedFiles = @(& git diff --cached --name-only)
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect staged files."
    }

    $forbidden = @($stagedFiles | Where-Object { Test-ForbiddenGitPath $_ })
    if ($forbidden.Count -gt 0) {
        Write-Host "Forbidden files are staged:" -ForegroundColor Red
        $forbidden | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        throw "Refusing to commit forbidden files."
    }

    if ($stagedFiles.Count -gt 0) {
        Write-Step "Committing source changes"
        Invoke-Checked "git" @("commit", "-m", $CommitMessage)
    } else {
        Write-Step "No staged changes to commit"
    }

    Write-Step "Creating and pushing release tag"
    Invoke-Checked "git" @("tag", $Tag)
    Invoke-Checked "git" @("push", $Remote, "main")
    Invoke-Checked "git" @("push", $Remote, $Tag)
}

Set-Location $RepoRoot

if (-not $SkipGit) {
    Write-Step "Pre-validating Git state"
    Assert-GitAvailable
}

Write-Step "Cleaning release publish directories"
Remove-DirectoryInsideRepo $FrameworkPublishRelative
Remove-DirectoryInsideRepo $SelfContainedPublishRelative

Write-Step "Running tests"
Invoke-Checked "dotnet" @("test", $TestProjectPath, "-c", "Release")

Write-Step "Running Release build"
Invoke-Checked "dotnet" @("build", $ProjectPath, "-c", "Release")

Write-Step "Restoring win-x64 runtime assets"
Invoke-Checked "dotnet" @("restore", $ProjectPath, "-r", "win-x64")

Write-Step "Publishing framework-dependent single-file build"
Invoke-Checked "dotnet" @(
    "publish",
    $ProjectPath,
    "-c", "Release",
    "--no-restore",
    "-r", "win-x64",
    "--self-contained", "false",
    "-p:PublishSingleFile=true",
    "-p:DebugSymbols=true",
    "-p:DebugType=portable",
    "-o", $FrameworkPublishDir
)

Write-Step "Publishing self-contained single-file build"
Invoke-Checked "dotnet" @(
    "publish",
    $ProjectPath,
    "-c", "Release",
    "--no-restore",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugSymbols=true",
    "-p:DebugType=portable",
    "-o", $SelfContainedPublishDir
)

Write-Step "Validating publish outputs"
Assert-FileExists (Join-Path $FrameworkPublishDir "TerminalShell.exe")
Assert-FileExists (Join-Path $SelfContainedPublishDir "TerminalShell.exe")
Write-Host "Framework-dependent: $FrameworkPublishDir"
Write-Host "Self-contained:      $SelfContainedPublishDir"

if ($SkipGit) {
    Write-Step "SkipGit enabled; Git commit/tag/push was skipped"
    exit 0
}

Invoke-GitRelease
